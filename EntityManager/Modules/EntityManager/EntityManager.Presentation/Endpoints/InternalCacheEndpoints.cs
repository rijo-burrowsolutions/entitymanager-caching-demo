// PURPOSE: an internal-only endpoint that SandboxWatcher (Tools/SandboxWatcher,
// a plain .NET console app - there is no Node.js anywhere in this demo) calls
// to invalidate-and-refill a key right after a real sandbox DB change, and
// also to drain near-expiry entries off the "ety:refresh:queue" stream for
// stale-while-revalidate refreshes. It does NOT duplicate any business logic -
// it re-runs the exact same real query handler used on a normal cache MISS
// (RawJson -> camelCase -> office/company join -> CDN picture default), then
// writes the fresh result straight back into Redis under the same key with
// the query's own TTL. Still read-only end to end: this never touches the
// database except via the same GET-only handlers every other endpoint
// already uses.
//
// Covers single-record "get" keys, "idlist" keys (the ID half of the
// list-fresh pattern), and "suggest" keys (typeahead) - all but "get" carry a
// variable, "&"-joined set of params instead of a single identifying param,
// so they're parsed into a dictionary and mapped back onto whichever query
// type matches (entity, operation). There is no plain "list" case - the
// whole-page /Agent/list-style endpoints were removed in favor of list-fresh,
// which only ever needs "get" and "idlist" refreshes.
namespace EntityManager.Presentation.Endpoints;

using System.Text.Json;
using Ag.Cache;
using EntityManager.Application.Queries;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using StackExchange.Redis;

public static class InternalCacheEndpoints
{
    public static void MapInternalCacheEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/internal/cache/refresh", Handle);
    }

    private static async Task<IResult> Handle(
        [FromServices] IConnectionMultiplexer redis,
        [FromServices] GetAgentQueryHandler agentHandler,
        [FromServices] GetOfficeQueryHandler officeHandler,
        [FromServices] GetCompanyQueryHandler companyHandler,
        [FromServices] GetAgentIdListQueryHandler agentIdListHandler,
        [FromServices] GetOfficeIdListQueryHandler officeIdListHandler,
        [FromServices] GetCompanyIdListQueryHandler companyIdListHandler,
        [FromServices] GetAgentSuggestionsQueryHandler agentSuggestHandler,
        [FromServices] GetOfficeSuggestionsQueryHandler officeSuggestHandler,
        [FromServices] GetCompanySuggestionsQueryHandler companySuggestHandler,
        string key,
        CancellationToken cancellationToken)
    {
        var parsed = ParseCacheKey(key);
        if (parsed is null)
            return Results.BadRequest(new { message = "Unrecognized cache key format for refresh", key });

        var (clientCode, entity, operation, p) = parsed.Value;

        object? result;
        TimeSpan ttl;

        switch (entity, operation)
        {
            case ("agent", "get"):
                var aq = new GetAgentQuery(
                    GetInt(p, "agentkey"), GetString(p, "seoname"), clientCode,
                    GetString(p, "firstname"), GetString(p, "lastname"), GetString(p, "fullname"), GetString(p, "email"));
                result = await agentHandler.Handle(aq, cancellationToken);
                ttl = aq.Ttl;
                break;

            case ("office", "get"):
                var oq = new GetOfficeQuery(
                    GetInt(p, "officekey"), clientCode,
                    GetString(p, "officename"), GetString(p, "city"), GetString(p, "email"));
                result = await officeHandler.Handle(oq, cancellationToken);
                ttl = oq.Ttl;
                break;

            case ("company", "get"):
                var cq = new GetCompanyQuery(
                    GetInt(p, "companykey"), clientCode,
                    GetString(p, "companyname"), GetString(p, "email"));
                result = await companyHandler.Handle(cq, cancellationToken);
                ttl = cq.Ttl;
                break;

            case ("agent", "idlist"):
                var ailq = new GetAgentIdListQuery(
                    clientCode, GetString(p, "fullname"), GetString(p, "seoname"),
                    GetInt(p, "pagenumber"), GetInt(p, "pagesize"));
                result = await agentIdListHandler.Handle(ailq, cancellationToken);
                ttl = ailq.Ttl;
                break;

            case ("office", "idlist"):
                var oilq = new GetOfficeIdListQuery(
                    clientCode, GetString(p, "officename"), GetInt(p, "parentcompany"),
                    GetInt(p, "pagenumber"), GetInt(p, "pagesize"));
                result = await officeIdListHandler.Handle(oilq, cancellationToken);
                ttl = oilq.Ttl;
                break;

            case ("company", "idlist"):
                var cilq = new GetCompanyIdListQuery(
                    clientCode, GetString(p, "companyname"),
                    GetInt(p, "pagenumber"), GetInt(p, "pagesize"));
                result = await companyIdListHandler.Handle(cilq, cancellationToken);
                ttl = cilq.Ttl;
                break;

            case ("agent", "suggest"):
                var asq = new GetAgentSuggestionsQuery(
                    GetString(p, "name"), clientCode, GetBool(p, "isteam"), GetString(p, "excludekeys"), GetInt(p, "pagelimit"));
                result = await agentSuggestHandler.Handle(asq, cancellationToken);
                ttl = asq.Ttl;
                break;

            case ("office", "suggest"):
                var osq = new GetOfficeSuggestionsQuery(
                    GetString(p, "name"), clientCode, GetString(p, "excludekeys"), GetInt(p, "pagelimit"));
                result = await officeSuggestHandler.Handle(osq, cancellationToken);
                ttl = osq.Ttl;
                break;

            case ("company", "suggest"):
                var csq = new GetCompanySuggestionsQuery(
                    GetString(p, "name"), clientCode, GetString(p, "excludekeys"), GetInt(p, "pagelimit"));
                result = await companySuggestHandler.Handle(csq, cancellationToken);
                ttl = csq.Ttl;
                break;

            default:
                return Results.BadRequest(new { message = $"No refresher wired up for {entity}/{operation}", key });
        }

        if (result is null)
            return Results.NotFound(); // row no longer matches (e.g. deleted) - let the old entry expire naturally

        var db = redis.GetDatabase();
        await db.StringSetAsync(key, JsonSerializer.Serialize(result, JsonSerializerOptions.Web), ttl);
        return Results.Ok();
    }

    private static int? GetInt(Dictionary<string, string> p, string key) =>
        p.TryGetValue(key, out var v) ? int.Parse(v) : null;

    private static string? GetString(Dictionary<string, string> p, string key) =>
        p.TryGetValue(key, out var v) ? v : null;

    private static bool? GetBool(Dictionary<string, string> p, string key) =>
        p.TryGetValue(key, out var v) ? bool.Parse(v) : null;

    // Parses "ety:{clientCode}:{entity}:{operation}:{param1}=value1&param2=value2..."
    // - works for both "get" keys (a single param) and "list"/"idlist" keys
    // (a variable, "&"-joined set of filter/pagination params), since
    // CacheKeyBuilder.Build produces the same shape either way.
    private static (string ClientCode, string Entity, string Operation, Dictionary<string, string> Params)? ParseCacheKey(string key)
    {
        var parts = key.Split(':');
        if (parts.Length != 5 || parts[0] != "ety")
            return null;

        var paramsDict = new Dictionary<string, string>();
        foreach (var pair in parts[4].Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = pair.Split('=', 2);
            if (kv.Length == 2)
                paramsDict[kv[0]] = kv[1];
        }

        return (parts[1], parts[2], parts[3], paramsDict);
    }
}
