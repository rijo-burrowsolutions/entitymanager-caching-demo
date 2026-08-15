// PURPOSE: the REAL ag-kit GetAgentListQuery + handler, with ICacheableQuery
// added. One-to-many query -> shorter TTL than the single-record Get, since
// a single changed agent can't be precisely invalidated out of every list
// permutation that might include it.
using System.Text.Json;
using EntityManager.Domain.Repositories;
using Mediator;
using Ag.Util.Json;
using Ag.Abstractions.Common;
using Ag.Abstractions.Extensions;
using Ag.Cache;

namespace EntityManager.Application.Queries;

public record GetAgentListQuery(
    int? AgentKey,
    string? ClientID,
    string? FullName,
    string? SeoName,
    int? PageNumber,
    int? PageSize)
    : IRequest<PagedResult<JsonElement>>, ICacheableQuery
{
    // See GetAgentQuery.cs - no ClientID means not cacheable for this call
    // (otherwise different real clients that all omit it would collide onto
    // the same "default"-scoped list cache entry).
    public string? BuildCacheKey() => string.IsNullOrWhiteSpace(ClientID) ? null : CacheKeyBuilder.Build(
        ClientID, "agent", "list",
        new Dictionary<string, string?>
        {
            ["agentKey"] = AgentKey?.ToString(),
            ["fullName"] = FullName,
            ["seoName"] = SeoName,
            ["pageNumber"] = PageNumber?.ToString(),
            ["pageSize"] = PageSize?.ToString()
        });

    // Flat 10 min locally for every cacheable query (this happens to match
    // real production's list TTL exactly - Complete Guide, Part F).
    public TimeSpan Ttl => TimeSpan.FromMinutes(10);
    public TimeSpan RefreshWindow => TimeSpan.FromMinutes(2);
}

public class GetAgentListQueryHandler
    : IRequestHandler<GetAgentListQuery, PagedResult<JsonElement>>
{
    private readonly IAgentRepository agentRepository;

    public GetAgentListQueryHandler(
        IAgentRepository agentRepository)
    {
        this.agentRepository = agentRepository;
    }

    public async ValueTask<PagedResult<JsonElement>> Handle(
        GetAgentListQuery request,
        CancellationToken cancellationToken)
    {
        var agents = agentRepository.GetAgentList(
            request.AgentKey,
            request.ClientID,
            request.FullName,
            request.SeoName,
            cancellationToken);

        return await agents
        .Select(a => a.RawJson)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => CamelCaseConversion.ConvertToCamelCase(x!))
        .ToPagedResultAsync(
            request.PageNumber ?? 1,
            request.PageSize ?? 20,
            cancellationToken);
    }
}
