// PURPOSE: watches the local idc_ety_sandbox LocalDB copy for real changes,
// invalidates the matching Redis cache key, then immediately refills it via
// /internal/cache/refresh so the very next reader gets a fresh HIT instead of
// a MISS. This is a plain .NET console app - there is no Node.js anywhere in
// this demo; this one process plays both the "watch for changes" and
// "refill near-expiry keys" roles itself. Run this alongside the API (with
// "UseSandboxDb": true in appsettings.json) to test a genuinely real write
// triggering invalidation, instead of simulating it by deleting a Redis key
// by hand.
//
// Usage: dotnet run --project Tools/SandboxWatcher
using Ag.Cache;
using Microsoft.Data.SqlClient;
using StackExchange.Redis;

const string SandboxConnectionString =
    @"Server=(localdb)\MSSQLLocalDB;Database=idc_ety_sandbox;Trusted_Connection=True;TrustServerCertificate=True;";
const int PollIntervalMs = 3000;

// Same stream CachingPipelineBehavior.cs pushes onto when a HIT is close to
// expiring. Nothing else in this demo reads that stream, so without the loop
// below near-expiry entries would just sit here unread and expire normally
// instead of being proactively refreshed. ConsumeRefreshQueueLoopAsync below
// fills that gap.
const string RefreshQueueKey = "ety:refresh:queue";
const string RefreshQueueCheckpointKey = "ety:sandbox:watcher:refreshqueue:checkpoint";

var redis = await ConnectionMultiplexer.ConnectAsync("127.0.0.1:6379");
var db = redis.GetDatabase();

// Used to immediately refill a key right after invalidating it, via the same
// /internal/cache/refresh endpoint the near-expiry SWR refresh loop below
// also calls - see RefreshCacheAsync below for why this is best-effort.
var httpClient = new HttpClient { BaseAddress = new Uri("http://localhost:5080") };

// (Table, business-key column, entity name used in the cache key format)
(string Table, string BusinessKeyColumn, string Entity)[] tables =
[
    ("Agent", "AGENTKEY", "agent"),
    ("Office", "OFFICEKEY", "office"),
    ("Company", "COMPANYKEY", "company"),
];

Console.WriteLine($"[sandbox-watcher] started, polling idc_ety_sandbox every {PollIntervalMs}ms");

await Task.WhenAll(PollDatabaseLoopAsync(), ConsumeRefreshQueueLoopAsync());

async Task PollDatabaseLoopAsync()
{
    while (true)
    {
        foreach (var (table, businessKeyColumn, entity) in tables)
        {
            try
            {
                await CheckTableAsync(table, businessKeyColumn, entity);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[sandbox-watcher] error checking {table}: {ex.Message}");
            }
        }
        await Task.Delay(PollIntervalMs);
    }
}

// The sandbox equivalent of refreshWorker.js: drains ety:refresh:queue and
// proactively refreshes each near-expiry key, the same way CheckTableAsync
// does after a detected DB change - reuses the same RefreshCacheAsync helper.
async Task ConsumeRefreshQueueLoopAsync()
{
    while (true)
    {
        try
        {
            await ConsumeRefreshQueueOnceAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[sandbox-watcher] error consuming refresh queue: {ex.Message}");
        }
        await Task.Delay(PollIntervalMs);
    }
}

async Task ConsumeRefreshQueueOnceAsync()
{
    var checkpoint = await db.StringGetAsync(RefreshQueueCheckpointKey);
    var lastId = checkpoint.IsNullOrEmpty ? "0-0" : checkpoint.ToString();

    var entries = await db.StreamReadAsync(RefreshQueueKey, lastId, count: 100);
    if (entries.Length == 0)
        return;

    foreach (var entry in entries)
    {
        var cacheKey = entry.Values.FirstOrDefault(v => v.Name == "key").Value;
        if (!cacheKey.IsNullOrEmpty)
        {
            Console.WriteLine($"[sandbox-watcher] near-expiry refresh for {cacheKey}");
            await RefreshCacheAsync(cacheKey!);
        }
        lastId = entry.Id!;
    }

    await db.StringSetAsync(RefreshQueueCheckpointKey, lastId);
}

// Separate checkpoint namespace ("ety:sandbox:...") from the real watcher.js's
// ("ety:demo:watcher:...") so running both at once never lets one process's
// checkpoint clobber the other's - they're watching two different databases.
async Task CheckTableAsync(string table, string businessKeyColumn, string entity)
{
    var checkpointKey = $"ety:sandbox:watcher:{table.ToLowerInvariant()}:checkpoint";
    var lastCheckpoint = await db.StringGetAsync(checkpointKey);

    using var conn = new SqlConnection(SandboxConnectionString);
    await conn.OpenAsync();

    if (lastCheckpoint.IsNullOrEmpty)
    {
        // First run - just record "now" as the starting point, nothing to diff yet.
        using var cmd = new SqlCommand($"SELECT MAX(LASTMODIFIED) AS MaxUpdated FROM {table}", conn);
        var result = await cmd.ExecuteScalarAsync();
        var maxUpdated = result as DateTime? ?? DateTime.UtcNow;
        await db.StringSetAsync(checkpointKey, maxUpdated.ToString("o"));
        return;
    }

    var checkpointValue = DateTime.Parse(lastCheckpoint!, null, System.Globalization.DateTimeStyles.RoundtripKind);

    using var selectCmd = new SqlCommand(
        $"SELECT {businessKeyColumn} AS BusinessKey, CLIENTCODE AS ClientCode, LASTMODIFIED FROM {table} WHERE LASTMODIFIED > @checkpoint ORDER BY LASTMODIFIED",
        conn);
    // AddWithValue would default this to SqlDbType.DateTime (legacy, ~3.33ms
    // rounding) instead of matching the column's actual datetime2(7)
    // precision - the rounded-down parameter value then sits perpetually
    // just behind the true stored LASTMODIFIED, so the same row would keep
    // matching "> @checkpoint" forever. Explicit DateTime2 fixes that.
    selectCmd.Parameters.Add("@checkpoint", System.Data.SqlDbType.DateTime2).Value = checkpointValue;

    var changed = new List<(int BusinessKey, string? ClientCode, DateTime LastModified)>();
    using (var reader = await selectCmd.ExecuteReaderAsync())
    {
        while (await reader.ReadAsync())
        {
            changed.Add((
                reader.GetInt32(0),
                reader.IsDBNull(1) ? null : reader.GetString(1),
                reader.GetDateTime(2)));
        }
    }

    if (changed.Count == 0)
        return;

    Console.WriteLine($"[sandbox-watcher] {changed.Count} change(s) in {table}");

    var maxSeen = checkpointValue;
    foreach (var row in changed)
    {
        if (row.ClientCode is not null)
        {
            var cacheKey = CacheKeyBuilder.Build(row.ClientCode, entity, "get",
                new Dictionary<string, string?> { [$"{entity}key"] = row.BusinessKey.ToString() });

            // Only refill if the key actually existed - otherwise this would
            // manufacture a brand-new cache entry for a row nobody has ever
            // queried, which isn't invalidation, it's cache warming for demand
            // that doesn't exist.
            var existed = await db.KeyDeleteAsync(cacheKey);
            if (existed)
            {
                Console.WriteLine($"[sandbox-watcher] invalidated {cacheKey}");
                await RefreshCacheAsync(cacheKey);
            }
        }

        if (row.LastModified > maxSeen)
            maxSeen = row.LastModified;
    }

    await db.StringSetAsync(checkpointKey, maxSeen.ToString("o"));
}

// Immediately refills the key we just deleted, so the next reader gets a fast
// HIT with the fresh data instead of paying a MISS. Best-effort on purpose:
// if the API isn't running (or the row no longer matches, e.g. deleted), the
// key is simply left empty - same as today, just no worse - and the next real
// request repopulates it as normal.
async Task RefreshCacheAsync(string cacheKey)
{
    try
    {
        var response = await httpClient.PostAsync(
            $"/internal/cache/refresh?key={Uri.EscapeDataString(cacheKey)}", content: null);

        if (response.IsSuccessStatusCode)
            Console.WriteLine($"[sandbox-watcher] refreshed {cacheKey}");
        else
            Console.WriteLine($"[sandbox-watcher] refresh skipped for {cacheKey} ({(int)response.StatusCode})");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[sandbox-watcher] refresh failed for {cacheKey}: {ex.Message}");
    }
}
