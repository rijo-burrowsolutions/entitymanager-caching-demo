// PURPOSE: the REAL ag-kit GetOfficeSuggestionsQuery + handler (typeahead
// over office names), with ICacheableQuery added - see GetAgentSuggestionsQuery.cs
// for the full reasoning (including why Handle() is implicit here, not explicit).
using Ag.Cache;
using EntityManager.Domain.Entities;
using EntityManager.Domain.Repositories;
using Mediator;

namespace EntityManager.Application.Queries;

public record GetOfficeSuggestionsQuery(string? Name, string? ClientCode, string? ExcludeKeys, int? PageLimit)
    : IRequest<List<Office>>, ICacheableQuery
{
    // Reflection-based - see GetAgentQuery.cs for the full reasoning.
    public string? BuildCacheKey() => CacheKeyBuilder.BuildFromObject(
        ClientCode, "office", "suggest", this, nameof(ClientCode));

    public TimeSpan Ttl => TimeSpan.FromMinutes(10);
    public TimeSpan RefreshWindow => TimeSpan.FromMinutes(2);
}

public class GetOfficeSuggestionsQueryHandler : IRequestHandler<GetOfficeSuggestionsQuery, List<Office>>
{
    private readonly IOfficeRepository officeRepository;

    public GetOfficeSuggestionsQueryHandler(IOfficeRepository officeRepository)
    {
        this.officeRepository = officeRepository;
    }

    public async ValueTask<List<Office>> Handle(GetOfficeSuggestionsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required");
        if (string.IsNullOrWhiteSpace(request.ClientCode))
            throw new ArgumentException("ClientCode is required");

        return await officeRepository.SuggestOffices(
            request.Name,
            request.ClientCode,
            SuggestionParams.ParseExcludeKeys(request.ExcludeKeys),
            SuggestionParams.Take(request.PageLimit),
            cancellationToken);
    }
}
