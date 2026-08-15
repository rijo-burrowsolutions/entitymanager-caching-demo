// PURPOSE: the "opt-in switch" for caching. Any Mediator query that wants to
// be cached implements this interface to say "cache me, here's my key, and
// here's how long to keep me." Queries that don't implement it are completely
// ignored by CachingPipelineBehavior - nothing about them changes.
namespace Ag.Cache;

public interface ICacheableQuery
{
    // Null means "not cacheable for THIS particular call" (e.g. a required
    // scoping value like ClientCode is missing) - CachingPipelineBehavior
    // then skips Redis entirely and just runs the real handler, same as a
    // query that never implemented this interface at all. This is what keeps
    // a cache entry from ever being created under a made-up fallback value
    // that nothing watching the database could ever reconstruct to invalidate.
    string? BuildCacheKey();
    TimeSpan Ttl { get; }
    TimeSpan RefreshWindow { get; }
}
