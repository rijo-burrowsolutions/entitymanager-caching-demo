// PURPOSE: the single place that decides what a cache key string looks like.
// Every cacheable query calls this instead of building its own string by
// hand, so the format is guaranteed identical everywhere. Sorting the
// parameters means the same logical request always produces the same key,
// no matter what order the parameters happened to arrive in.
using System.Reflection;

namespace Ag.Cache;

public static class CacheKeyBuilder
{
    // ICacheableQuery's own members (Ttl, RefreshWindow) are plumbing, not
    // request params - every query implements them, so without this they'd
    // get swept into every reflection-built key (e.g. "...&ttl=00:10:00").
    // Excluded here once, automatically, rather than relying on every query
    // that adopts BuildFromObject to remember to exclude them individually.
    private static readonly HashSet<string> InterfaceOwnedProperties = new(
        typeof(ICacheableQuery)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name),
        StringComparer.OrdinalIgnoreCase);

    // Reflection-based alternative to Build(): reads every public property
    // off the query record itself instead of a hand-built dictionary, so a
    // new property added to the record is automatically part of the key with
    // no BuildCacheKey() edit required. Pass the scope property's own name
    // (e.g. "ClientCode") in excludeProperties so it isn't duplicated into
    // the param list - it's already the {clientCode} segment of the key.
    // Mark any other non-filter property with [CacheKeyIgnore] to keep it out
    // of the key entirely (reflection otherwise has no way to know a property
    // isn't meant to scope the cache).
    public static string? BuildFromObject(
        string? clientCode,
        string entity,
        string operation,
        object query,
        params string[] excludeProperties)
    {
        if (string.IsNullOrWhiteSpace(clientCode))
            return null;

        var exclude = new HashSet<string>(excludeProperties, StringComparer.OrdinalIgnoreCase);

        var parameters = query.GetType()
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => !exclude.Contains(p.Name))
            .Where(p => !InterfaceOwnedProperties.Contains(p.Name))
            .Where(p => p.GetCustomAttribute<CacheKeyIgnoreAttribute>() is null)
            .ToDictionary(p => p.Name, p => p.GetValue(query)?.ToString());

        return Build(clientCode, entity, operation, parameters);
    }

    // Builds: ety:{clientCode}:{entity}:{operation}:{sorted params}
    public static string Build(
        string clientCode,
        string entity,
        string operation,
        IDictionary<string, string?> parameters)
    {
        // Every param VALUE is lowercased too, not just its name - SQL Server's
        // default collation compares strings case-insensitively (SeoName,
        // FullName, OfficeName, CompanyName, Name...), so "Rijo", "rijo" and
        // "riJO" all match the exact same row(s) in the database. Without
        // normalizing here, each casing would produce its own separate Redis
        // key for what the database considers one identical query - and since
        // SandboxWatcher rebuilds keys from the row's own real, canonical
        // value (also normalized the same way, via this same method), only
        // whichever casing happens to match that would ever be reachable for
        // invalidation. This is the exact same problem ClientCode's uppercasing
        // below already solves for the tenant segment - applied here to every
        // other value too, in one place, instead of per-field.
        var normalized = parameters
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .OrderBy(p => p.Key, StringComparer.OrdinalIgnoreCase)
            .Select(p => $"{p.Key.ToLowerInvariant()}={p.Value!.ToLowerInvariant()}");

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
