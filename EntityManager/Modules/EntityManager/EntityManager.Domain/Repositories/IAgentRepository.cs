// PURPOSE: the REAL ag-kit IAgentRepository contract - GetAgentDetail joins
// Agent -> Office -> Company for the single-record endpoint; GetAgentList
// returns an IQueryable so paging/counting happens in one SQL round trip
// per call, not by loading everything into memory first.
using EntityManager.Domain.Entities;

namespace EntityManager.Domain.Repositories;

public interface IAgentRepository
{
    Task<Agent> GetAgent(int? agentKey, string? seoName, string? clientCode, CancellationToken cancellationToken);
    Task<AgentDetail> GetAgentDetail(
        int? agentKey, string? seoName, string? clientCode,
        string? firstName, string? lastName, string? fullName, string? email,
        CancellationToken cancellationToken);
    Task<Agent?> UpdateAgent(
        int agentKey, string clientCode,
        string? firstName, string? lastName, string? fullName, string? emailAddress,
        CancellationToken cancellationToken);
    IQueryable<Agent> GetAgentList(
        int? agentKey,
        string? clientCode,
        string? fullName,
        string? seoName,
        CancellationToken cancellationToken);
    Task<List<Agent>> SuggestAgents(
        string name,
        string clientCode,
        bool? isTeam,
        IReadOnlyCollection<int> excludeKeys,
        int take,
        CancellationToken cancellationToken);
}
