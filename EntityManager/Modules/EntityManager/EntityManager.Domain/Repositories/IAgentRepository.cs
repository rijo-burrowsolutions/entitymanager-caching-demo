// PURPOSE: the REAL ag-kit IAgentRepository contract - GetAgentDetail joins
// Agent -> Office -> Company for the single-record endpoint; GetAgentList
// returns an IQueryable so paging/counting happens in one SQL round trip
// per call, not by loading everything into memory first.
using EntityManager.Domain.Entities;

namespace EntityManager.Domain.Repositories;

public interface IAgentRepository
{
    Task<Agent> GetAgent(int? agentKey, string? seoName, string? clientCode, CancellationToken cancellationToken);
    Task<AgentDetail> GetAgentDetail(int? agentKey, string? seoName, string? clientCode, CancellationToken cancellationToken);
    IQueryable<Agent> GetAgentList(
        int? agentKey,
        string? clientCode,
        string? fullName,
        string? seoName,
        CancellationToken cancellationToken);
}
