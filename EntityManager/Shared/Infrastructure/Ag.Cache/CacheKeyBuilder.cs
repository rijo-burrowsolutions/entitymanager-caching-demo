// PURPOSE: the single place that decides what a cache key string looks like.
// Every cacheable query calls this instead of building its own string by
// hand, so the format is guaranteed identical everywhere. Sorting the
// parameters means the same logical request always produces the same key,
// no matter what order the parameters happened to arrive in.
namespace Ag.Cache;

public static class CacheKeyBuilder
{
    // Builds: ety:{clientCode}:{entity}:{operation}:{sorted params}
    public static string Build(
        string clientCode,
        string entity,
        string operation,
        IDictionary<string, string?> parameters)
    {
        var normalized = parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => $"{p.Key.ToLowerInvariant()}={p.Value}");

        var paramPart = string.Join("&", normalized);

        // ClientCode is a database identity value, not free text - SQL Server
        // compares it case-insensitively ('VLA' = 'vla'), but a Redis key is
        // just a string and doesn't know that. Without normalizing here, a
        // caller who types "vla" and one who types "VLA" get two totally
        // separate cache entries for the same real tenant, and whichever
        // casing the database itself happens to store is the only one
        // SandboxWatcher/watcher.js can ever invalidate - anything else is
        // permanently stale. Uppercasing here guarantees every caller, and
        // the watcher, always land on the exact same key.
        return $"ety:{clientCode.ToUpperInvariant()}:{entity}:{operation}:{paramPart}";
    }
}
