// PURPOSE: the REAL ag-kit GetCompanyListQuery + handler, with ICacheableQuery added.
using System.Text.Json;
using EntityManager.Domain.Repositories;
using Mediator;
using Ag.Abstractions.Common;
using Ag.Abstractions.Extensions;
using Ag.Util.Json;
using Ag.Cache;

namespace EntityManager.Application.Queries;

public record GetCompanyListQuery(
    int? CompanyKey,
    string? ClientID,
    string? CompanyName,
    int? PageNumber,
    int? PageSize)
    : IRequest<PagedResult<JsonElement>>, ICacheableQuery
{
    // See GetAgentQuery.cs - no ClientID means not cacheable for this call.
    public string? BuildCacheKey() => string.IsNullOrWhiteSpace(ClientID) ? null : CacheKeyBuilder.Build(
        ClientID, "company", "list",
        new Dictionary<string, string?>
        {
            ["companyKey"] = CompanyKey?.ToString(),
            ["companyName"] = CompanyName,
            ["pageNumber"] = PageNumber?.ToString(),
            ["pageSize"] = PageSize?.ToString()
        });

    // Flat 10 min locally for every cacheable query - see GetAgentQuery.cs.
    public TimeSpan Ttl => TimeSpan.FromMinutes(10);
    public TimeSpan RefreshWindow => TimeSpan.FromMinutes(2);
}

public class GetCompanyListQueryHandler
    : IRequestHandler<GetCompanyListQuery, PagedResult<JsonElement>>
{
    private readonly ICompanyRepository companyRepository;

    public GetCompanyListQueryHandler(
        ICompanyRepository companyRepository)
    {
        this.companyRepository = companyRepository;
    }

    public async ValueTask<PagedResult<JsonElement>> Handle(
        GetCompanyListQuery request,
        CancellationToken cancellationToken)
    {
        var companies = companyRepository.GetCompanyList(
            request.CompanyKey,
            request.ClientID,
            request.CompanyName,
            cancellationToken);

        return await companies
        .Where(c => !string.IsNullOrWhiteSpace(c.RawJson))
        .Select(c => CamelCaseConversion.ConvertToCamelCase(c.RawJson!))
        .ToPagedResultAsync(
            request.PageNumber ?? 1,
            request.PageSize ?? 20,
            cancellationToken);
    }
}
