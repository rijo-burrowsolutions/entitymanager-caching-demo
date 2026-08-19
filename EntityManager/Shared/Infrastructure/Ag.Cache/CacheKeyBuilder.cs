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

    // A single row's data can end up cached under many different key shapes -
    // "agentkey=150", "agentkey=150&firstname=abbie", "email=x@y.com" - one
    // per distinct filter combination a caller happened to use. When the row
    // changes, whoever's invalidating only knows the row's real business key
    // (e.g. AgentKey=150); it has no way to look up which of those other key
    // shapes exist in Redis, since some of them (email=, firstname=...) were
    // built from values that may no longer match anything in the database.
    // This index key names a Redis Set that CachingPipelineBehavior adds every
    // such key into (whenever the key includes "{entity}key=", i.e. the
    // caller's business key was known), so SandboxWatcher can look up and
    // invalidate every shape ever cached for that row - not just the one
    // shape it knows how to rebuild itself.
    public static string IndexKey(string entity, string businessKey) =>
        $"ety:index:{entity}:{businessKey.ToLowerInvariant()}";

    // Extracts the IndexKey for a fully-built cache key, if it carries the
    // entity's own business key as one of its params - returns null for keys
    // built purely from other filters (e.g. email-only lookups with no
    // AgentKey supplied), which simply can't be indexed this way since the
    // row they resolve to isn't known until the query actually runs.
    public static string? IndexKeyFor(string cacheKey)
    {
        var parts = cacheKey.Split(':');
        if (parts.Length != 5 || parts[0] != "ety")
            return null;

        var entity = parts[2];
        foreach (var pair in parts[4].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2 && kv[0] == $"{entity}key")
                return IndexKey(entity, kv[1]);
        }

        return null;
    }
}
