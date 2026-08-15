// PURPOSE: the side-by-side comparison query for /Agent/list-fresh. Instead
// of caching one big rendered page (GetAgentListQuery's approach - fast, but
// stale until the whole page's TTL expires even if only one agent in it
// changed), this composes two ALREADY-cached queries:
//   1. GetAgentIdListQuery  - which AgentKeys are on this page (cheap, low-risk if briefly stale)
//   2. GetAgentQuery (once per ID) - the exact same per-record cache /Agent/get
//      uses, which watcher.js already invalidates precisely when that one
//      agent's row changes.
// Net effect: if agent X was just updated, every OTHER agent on the page is
// still a cache hit, and ONLY agent X is a fresh read - never stale data,
// without needing to cache (or invalidate) the whole page as one unit.
//
// This query is deliberately NOT itself an ICacheableQuery - caching its
// assembled output again would just reintroduce the exact staleness problem
// this pattern exists to avoid. All of its speed comes from its two cached
// sub-queries; CachingPipelineBehavior sees this type doesn't implement
// ICacheableQuery and passes it straight through to this handler every time.
using System.Text.Json;
using Ag.Abstractions.Common;
using Mediator;

namespace EntityManager.Application.Queries;

public record GetAgentListFreshQuery(
    string? ClientID,
    string? FullName,
    string? SeoName,
    int? PageNumber,
    int? PageSize)
    : IRequest<PagedResult<JsonElement>>;

public class GetAgentListFreshQueryHandler : IRequestHandler<GetAgentListFreshQuery, PagedResult<JsonElement>>
{
    private readonly ISender sender;

    public GetAgentListFreshQueryHandler(ISender sender)
    {
        this.sender = sender;
    }

    public async ValueTask<PagedResult<JsonElement>> Handle(GetAgentListFreshQuery request, CancellationToken cancellationToken)
    {
        var idPage = await sender.Send(
            new GetAgentIdListQuery(request.ClientID, request.FullName, request.SeoName, request.PageNumber, request.PageSize),
            cancellationToken);

        var items = new List<JsonElement>(idPage.Items.Count);

        // Sequential on purpose: these all share the same scoped EF Core
        // DbContext under the hood (via IAgentRepository), which is not
        // thread-safe for concurrent queries - running these with
        // Task.WhenAll would throw "a second operation was started on this
        // context before a previous operation completed" on any real MISS.
        foreach (var agentKey in idPage.Items)
        {
            var agent = await sender.Send(new GetAgentQuery(agentKey, null, request.ClientID), cancellationToken);
            if (agent.HasValue)
                items.Add(agent.Value);
        }

        return new PagedResult<JsonElement>
        {
            Items = items,
            TotalCount = idPage.TotalCount,
            PageNumber = idPage.PageNumber,
            PageSize = idPage.PageSize
        };
    }
}
