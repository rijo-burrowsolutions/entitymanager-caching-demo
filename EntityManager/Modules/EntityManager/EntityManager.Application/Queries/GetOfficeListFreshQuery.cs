// PURPOSE: the Office equivalent of GetAgentListFreshQuery.cs - composes
// GetOfficeIdListQuery (cached ID list) with per-record GetOfficeQuery calls
// (the same cache /Office/get uses, precisely invalidated by watcher.js) so
// an updated office is never served stale here, unlike plain /Office/list.
// Deliberately NOT itself an ICacheableQuery - see GetAgentListFreshQuery.cs
// for why caching the assembled page again would defeat the point.
using System.Text.Json;
using Ag.Abstractions.Common;
using Mediator;

namespace EntityManager.Application.Queries;

public record GetOfficeListFreshQuery(
    string? ClientID,
    string? OfficeName,
    int? ParentCompany,
    int? PageNumber,
    int? PageSize)
    : IRequest<PagedResult<JsonElement>>;

public class GetOfficeListFreshQueryHandler : IRequestHandler<GetOfficeListFreshQuery, PagedResult<JsonElement>>
{
    private readonly ISender sender;

    public GetOfficeListFreshQueryHandler(ISender sender)
    {
        this.sender = sender;
    }

    public async ValueTask<PagedResult<JsonElement>> Handle(GetOfficeListFreshQuery request, CancellationToken cancellationToken)
    {
        var idPage = await sender.Send(
            new GetOfficeIdListQuery(request.ClientID, request.OfficeName, request.ParentCompany, request.PageNumber, request.PageSize),
            cancellationToken);

        var items = new List<JsonElement>(idPage.Items.Count);

        // Sequential on purpose - see GetAgentListFreshQuery.cs for why
        // (shared scoped EF Core DbContext is not thread-safe for concurrent queries).
        foreach (var officeKey in idPage.Items)
        {
            var office = await sender.Send(new GetOfficeQuery(officeKey, request.ClientID), cancellationToken);
            if (office.HasValue)
                items.Add(office.Value);
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
