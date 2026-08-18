// PURPOSE: the REAL ag-kit GetAgentSuggestionsQuery + handler (typeahead over
// agent names), with ICacheableQuery added - real ag-kit doesn't cache this
// query today, but it's a perfect example of "a different-shaped Get API"
// for the same entity: distinct params (Name/IsTeam/ExcludeKeys/PageLimit)
// from GetAgentQuery's (AgentKey/SeoName), same reflection-based caching.
//
// Also deviates from real ag-kit in one small way: the real handler uses an
// EXPLICIT interface implementation (IRequestHandler<...>.Handle(...)),
// which can only be called by casting to the interface first. This demo uses
// a normal (implicit) method instead, purely so InternalCacheEndpoints.cs
// can call agentSuggestHandler.Handle(...) directly like every other handler
// - no behavior difference, just easier to wire up consistently.
using Ag.Cache;
using EntityManager.Domain.Entities;
using EntityManager.Domain.Repositories;
using Mediator;

namespace EntityManager.Application.Queries;

// IsTeam: true -> teams only, false -> agents only, null -> both. ExcludeKeys: csv of already-selected keys.
public record GetAgentSuggestionsQuery(string? Name, string? ClientCode, bool? IsTeam, string? ExcludeKeys, int? PageLimit)
    : IRequest<List<Agent>>, ICacheableQuery
{
    // Reflection-based - see GetAgentQuery.cs for the full reasoning.
    public string? BuildCacheKey() => CacheKeyBuilder.BuildFromObject(
        ClientCode, "agent", "suggest", this, nameof(ClientCode));

    public TimeSpan Ttl => TimeSpan.FromMinutes(10);
    public TimeSpan RefreshWindow => TimeSpan.FromMinutes(2);
}

public class GetAgentSuggestionsQueryHandler : IRequestHandler<GetAgentSuggestionsQuery, List<Agent>>
{
    private readonly IAgentRepository agentRepository;

    public GetAgentSuggestionsQueryHandler(IAgentRepository agentRepository)
    {
        this.agentRepository = agentRepository;
    }

    public async ValueTask<List<Agent>> Handle(GetAgentSuggestionsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required");
        if (string.IsNullOrWhiteSpace(request.ClientCode))
            throw new ArgumentException("ClientCode is required");

        return await agentRepository.SuggestAgents(
            request.Name,
            request.ClientCode,
            request.IsTeam,
            SuggestionParams.ParseExcludeKeys(request.ExcludeKeys),
            SuggestionParams.Take(request.PageLimit),
            cancellationToken);
    }
}
