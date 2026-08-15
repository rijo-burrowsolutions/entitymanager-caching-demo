// PURPOSE: the REAL ag-kit GetOfficeListQuery + handler, with ICacheableQuery added.
using System.Text.Json;
using EntityManager.Domain.Repositories;
using Mediator;
using Ag.Abstractions.Common;
using Ag.Abstractions.Extensions;
using Ag.Util.Json;
using Ag.Cache;

namespace EntityManager.Application.Queries;

public record GetOfficeListQuery(
    int? OfficeKey,
    string? ClientID,
    string? officename,
    int? parentcompany,
    int? PageNumber,
    int? PageSize)
    : IRequest<PagedResult<JsonElement>>, ICacheableQuery
{
    // See GetAgentQuery.cs - no ClientID means not cacheable for this call.
    public string? BuildCacheKey() => string.IsNullOrWhiteSpace(ClientID) ? null : CacheKeyBuilder.Build(
        ClientID, "office", "list",
        new Dictionary<string, string?>
        {
            ["officeKey"] = OfficeKey?.ToString(),
            ["officename"] = officename,
            ["parentcompany"] = parentcompany?.ToString(),
            ["pageNumber"] = PageNumber?.ToString(),
            ["pageSize"] = PageSize?.ToString()
        });

    // Flat 10 min locally for every cacheable query - see GetAgentQuery.cs.
    public TimeSpan Ttl => TimeSpan.FromMinutes(10);
    public TimeSpan RefreshWindow => TimeSpan.FromMinutes(2);
}

public class GetOfficeListQueryHandler
    : IRequestHandler<GetOfficeListQuery, PagedResult<JsonElement>>
{
    private readonly IOfficeRepository officeRepository;

    public GetOfficeListQueryHandler(
        IOfficeRepository officeRepository)
    {
        this.officeRepository = officeRepository;
    }

    public async ValueTask<PagedResult<JsonElement>> Handle(
        GetOfficeListQuery request,
        CancellationToken cancellationToken)
    {
        var offices = officeRepository.GetOfficeList(
            request.OfficeKey,
            request.ClientID,
            request.officename,
            request.parentcompany,
            cancellationToken);

        var paged = await offices
        .Select(o => o.RawJson)
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .ToPagedResultAsync(
            request.PageNumber ?? 1,
            request.PageSize ?? 20,
            cancellationToken);

        return new PagedResult<JsonElement>
        {
            Items = paged.Items
                .Select(x => CamelCaseConversion.ConvertToCamelCase(x!))
                .ToList(),

            TotalCount = paged.TotalCount,
            PageNumber = paged.PageNumber,
            PageSize = paged.PageSize
        };
    }
}
