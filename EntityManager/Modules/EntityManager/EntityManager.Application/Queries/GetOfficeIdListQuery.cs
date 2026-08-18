// PURPOSE: the Office equivalent of GetAgentIdListQuery.cs - caches only the
// matching OfficeKeys for a filter+page, never the full office content. See
// GetOfficeListFreshQuery.cs for how this combines with per-record caching.
using Ag.Abstractions.Common;
using Ag.Abstractions.Extensions;
using Ag.Cache;
using EntityManager.Domain.Repositories;
using Mediator;

namespace EntityManager.Application.Queries;

public record GetOfficeIdListQuery(
    string? ClientID,
    string? OfficeName,
    int? ParentCompany,
    int? PageNumber,
    int? PageSize)
    : IRequest<PagedResult<int>>, ICacheableQuery
{
    // Reflection-based - see GetAgentQuery.cs for the full reasoning.
    public string? BuildCacheKey() => CacheKeyBuilder.BuildFromObject(
        ClientID, "office", "idlist", this, nameof(ClientID));

    // Flat 10 min locally for every cacheable query - see GetAgentQuery.cs.
    public TimeSpan Ttl => TimeSpan.FromMinutes(10);
    public TimeSpan RefreshWindow => TimeSpan.FromMinutes(2);
}

public class GetOfficeIdListQueryHandler : IRequestHandler<GetOfficeIdListQuery, PagedResult<int>>
{
    private readonly IOfficeRepository officeRepository;

    public GetOfficeIdListQueryHandler(IOfficeRepository officeRepository)
    {
        this.officeRepository = officeRepository;
    }

    public async ValueTask<PagedResult<int>> Handle(GetOfficeIdListQuery request, CancellationToken cancellationToken)
    {
        var offices = officeRepository.GetOfficeList(null, request.ClientID, request.OfficeName, request.ParentCompany, cancellationToken);

        return await offices
            .Select(o => o.OfficeKey)
            .ToPagedResultAsync(request.PageNumber ?? 1, request.PageSize ?? 20, cancellationToken);
    }
}
