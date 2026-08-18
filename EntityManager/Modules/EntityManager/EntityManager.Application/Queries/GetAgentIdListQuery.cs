// PURPOSE: the "ID list" half of the ID-list + per-record caching pattern.
// Caches ONLY the matching AgentKeys for a filter+page (a few bytes), never
// the full agent content - so even if this entry is briefly stale, the
// worst case is a newly added/removed agent showing up a bit late, never
// wrong data for an agent that still matches. See GetAgentListFreshQuery.cs
// for how this gets combined with per-record caching.
using Ag.Abstractions.Common;
using Ag.Abstractions.Extensions;
using Ag.Cache;
using EntityManager.Domain.Repositories;
using Mediator;

namespace EntityManager.Application.Queries;

public record GetAgentIdListQuery(
    string? ClientID,
    string? FullName,
    string? SeoName,
    int? PageNumber,
    int? PageSize)
    : IRequest<PagedResult<int>>, ICacheableQuery
{
    // Reflection-based - see GetAgentQuery.cs for the full reasoning.
    public string? BuildCacheKey() => CacheKeyBuilder.BuildFromObject(
        ClientID, "agent", "idlist", this, nameof(ClientID));

    // Flat 10 min locally for every cacheable query - see GetAgentQuery.cs.
    public TimeSpan Ttl => TimeSpan.FromMinutes(10);
    public TimeSpan RefreshWindow => TimeSpan.FromMinutes(2);
}

public class GetAgentIdListQueryHandler : IRequestHandler<GetAgentIdListQuery, PagedResult<int>>
{
    private readonly IAgentRepository agentRepository;

    public GetAgentIdListQueryHandler(IAgentRepository agentRepository)
    {
        this.agentRepository = agentRepository;
    }

    public async ValueTask<PagedResult<int>> Handle(GetAgentIdListQuery request, CancellationToken cancellationToken)
    {
        var agents = agentRepository.GetAgentList(null, request.ClientID, request.FullName, request.SeoName, cancellationToken);

        // Projecting to just AgentKey (an int) keeps this 100% server-translatable -
        // unlike the RawJson list query, there's no client-side camelCase step here.
        return await agents
            .Select(a => a.AgentKey)
            .ToPagedResultAsync(request.PageNumber ?? 1, request.PageSize ?? 20, cancellationToken);
    }
}
