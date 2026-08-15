// PURPOSE: the Company equivalent of GetAgentListFreshQuery.cs - composes
// GetCompanyIdListQuery (cached ID list) with per-record GetCompanyQuery
// calls (the same cache /Company/get uses, precisely invalidated by
// watcher.js) so an updated company is never served stale here, unlike
// plain /Company/list. Deliberately NOT itself an ICacheableQuery - see
// GetAgentListFreshQuery.cs for why caching the assembled page again would
// defeat the point.
using System.Text.Json;
using Ag.Abstractions.Common;
using Mediator;

namespace EntityManager.Application.Queries;

public record GetCompanyListFreshQuery(
    string? ClientID,
    string? CompanyName,
    int? PageNumber,
    int? PageSize)
    : IRequest<PagedResult<JsonElement>>;

public class GetCompanyListFreshQueryHandler : IRequestHandler<GetCompanyListFreshQuery, PagedResult<JsonElement>>
{
    private readonly ISender sender;

    public GetCompanyListFreshQueryHandler(ISender sender)
    {
        this.sender = sender;
    }

    public async ValueTask<PagedResult<JsonElement>> Handle(GetCompanyListFreshQuery request, CancellationToken cancellationToken)
    {
        var idPage = await sender.Send(
            new GetCompanyIdListQuery(request.ClientID, request.CompanyName, request.PageNumber, request.PageSize),
            cancellationToken);

        var items = new List<JsonElement>(idPage.Items.Count);

        // Sequential on purpose - see GetAgentListFreshQuery.cs for why
        // (shared scoped EF Core DbContext is not thread-safe for concurrent queries).
        foreach (var companyKey in idPage.Items)
        {
            var company = await sender.Send(new GetCompanyQuery(companyKey, request.ClientID), cancellationToken);
            if (company.HasValue)
                items.Add(company.Value);
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
