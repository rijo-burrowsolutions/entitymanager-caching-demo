// PURPOSE: opt-out marker for CacheKeyBuilder.BuildFromObject. Reflection
// picks up every public property on a query record by default, which is
// right for filter/param properties but wrong for anything added later that
// isn't part of the request's identity (e.g. a property computed only for
// internal use) - tagging it with this attribute keeps it out of the key
// instead of silently fragmenting the cache.
namespace Ag.Cache;

[AttributeUsage(AttributeTargets.Property)]
public sealed class CacheKeyIgnoreAttribute : Attribute
{
}
