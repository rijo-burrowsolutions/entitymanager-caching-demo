// PURPOSE: the Company equivalent of GetAgentIdListQuery.cs - caches only
// the matching CompanyKeys for a filter+page, never the full company
// content. See GetCompanyListFreshQuery.cs for how this combines with
// per-record caching.
using Ag.Abstractions.Common;
using Ag.Abstractions.Extensions;
using Ag.Cache;
using EntityManager.Domain.Repositories;
using Mediator;

namespace EntityManager.Application.Queries;

public record GetCompanyIdListQuery(
    string? ClientID,
    string? CompanyName,
    int? PageNumber,
    int? PageSize)
    : IRequest<PagedResult<int>>, ICacheableQuery
{
    // See GetAgentQuery.cs - no ClientID means not cacheable for this call.
    public string? BuildCacheKey() => string.IsNullOrWhiteSpace(ClientID) ? null : CacheKeyBuilder.Build(
        ClientID, "company", "idlist",
        new Dictionary<string, string?>
        {
            ["companyName"] = CompanyName,
            ["pageNumber"] = PageNumber?.ToString(),
            ["pageSize"] = PageSize?.ToString()
        });

    // Flat 10 min locally for every cacheable query - see GetAgentQuery.cs.
    public TimeSpan Ttl => TimeSpan.FromMinutes(10);
    public TimeSpan RefreshWindow => TimeSpan.FromMinutes(2);
}

public class GetCompanyIdListQueryHandler : IRequestHandler<GetCompanyIdListQuery, PagedResult<int>>
{
    private readonly ICompanyRepository companyRepository;

    public GetCompanyIdListQueryHandler(ICompanyRepository companyRepository)
    {
        this.companyRepository = companyRepository;
    }

    public async ValueTask<PagedResult<int>> Handle(GetCompanyIdListQuery request, CancellationToken cancellationToken)
    {
        var companies = companyRepository.GetCompanyList(null, request.ClientID, request.CompanyName, cancellationToken);

        return await companies
            .Select(c => c.CompanyKey)
            .ToPagedResultAsync(request.PageNumber ?? 1, request.PageSize ?? 20, cancellationToken);
    }
}
