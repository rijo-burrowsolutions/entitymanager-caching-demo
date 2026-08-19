// PURPOSE: this is the actual caching logic - a Mediator pipeline behavior
// that Mediator automatically runs for EVERY request, right before the real
// query handler would run. On a cache HIT it returns instantly without ever
// touching the database; on a MISS it lets the real handler run as normal,
// then saves the result to Redis afterwards. If a HIT is close to expiring,
// it also fires a background refresh job - this is what makes the
// Stale-While-Revalidate pattern actually happen.
namespace Ag.Cache;

using Mediator;
using StackExchange.Redis;
using System.Text.Json;

public sealed class CachingPipelineBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IConnectionMultiplexer _redis;

    // Injected automatically by DI - registered once in AgCachingServiceExtensions.cs.
    public CachingPipelineBehavior(IConnectionMultiplexer redis) => _redis = redis;

    // NOTE: the real Mediator.Abstractions IPipelineBehavior signature takes
    // `next` BEFORE `cancellationToken` - the opposite order shown in the
    // design docs. Confirmed by an actual compiler error while building this
    // demo; the docs should be corrected to match this signature.
    public async ValueTask<TResponse> Handle(
        TRequest request,
        MessageHandlerDelegate<TRequest, TResponse> next,
        CancellationToken cancellationToken)
    {
        // Not every query opts in - pass through untouched if it doesn't implement ICacheableQuery.
        if (request is not ICacheableQuery cacheable)
            return await next(request, cancellationToken);

        IDatabase db;
        try
        {
            db = _redis.GetDatabase();
        }
        catch
        {
            Console.WriteLine("[cache] Redis unreachable - failing open, skipping cache");
            return await next(request, cancellationToken);
        }

        // A null key means this specific call is missing something caching
        // needs (e.g. no ClientCode) - skip Redis entirely rather than fall
        // back to a made-up key nothing else could ever reconstruct to
        // invalidate. Falls through to the real handler, uncached, same as a
        // query that never implemented ICacheableQuery at all.
        var key = cacheable.BuildCacheKey();
        if (key is null)
        {
            Console.WriteLine("[cache] SKIP (not cacheable for this call - missing scoping value)");
            return await next(request, cancellationToken);
        }

        // Ask Redis: do you have this key, and how much time is left on it?
        var cached = await db.StringGetWithExpiryAsync(key);

        if (cached.Value.HasValue)
        {
            // CACHE HIT - answer immediately, the real handler never runs.
            var remaining = cached.Expiry ?? TimeSpan.Zero;
            Console.WriteLine($"[cache] HIT  {key}  ({remaining.TotalSeconds:F0}s left)");

            // Near expiry? Queue a background refresh (fire-and-forget) so the
            // NEXT request gets fresh data instantly instead of a slow miss.
            if (cached.Expiry.HasValue && cached.Expiry.Value < cacheable.RefreshWindow)
            {
                Console.WriteLine($"[cache] near expiry - queuing background refresh for {key}");
                _ = db.StreamAddAsync("ety:refresh:queue", new NameValueEntry[] { new("key", key) });
            }

            return JsonSerializer.Deserialize<TResponse>((string)cached.Value!, JsonSerializerOptions.Web)!;
        }

        // CACHE MISS - this is the ORIGINAL code path, completely unchanged.
        Console.WriteLine($"[cache] MISS {key}");
        var result = await next(request, cancellationToken);

        // Save the fresh result so the next request for this exact key is a HIT.
        await db.StringSetAsync(key, JsonSerializer.Serialize(result, JsonSerializerOptions.Web), cacheable.Ttl);
        Console.WriteLine($"[cache] STORE {key}  (TTL {cacheable.Ttl.TotalSeconds:F0}s)");

        // Track this key under a reverse index keyed by the row's own business
        // key (when the key carries one), so an invalidator that only knows
        // "AgentKey 150 changed" can still find and clear every other filter
        // combination (email=, firstName=, etc.) cached for that same row -
        // see CacheKeyBuilder.IndexKeyFor for why this can't be derived from
        // the row's current data alone. Expiry mirrors the data key's own TTL
        // so the index entry never outlives what it's indexing.
        var indexKey = CacheKeyBuilder.IndexKeyFor(key);
        if (indexKey is not null)
        {
            await db.SetAddAsync(indexKey, key);
            await db.KeyExpireAsync(indexKey, cacheable.Ttl);
        }

        return result;
    }
}
