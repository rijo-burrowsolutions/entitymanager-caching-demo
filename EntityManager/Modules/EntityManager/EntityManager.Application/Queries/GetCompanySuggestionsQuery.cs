// PURPOSE: the REAL ag-kit GetCompanySuggestionsQuery + handler (typeahead
// over company names), with ICacheableQuery added - see GetAgentSuggestionsQuery.cs
// for the full reasoning (including why Handle() is implicit here, not explicit).
using Ag.Cache;
using EntityManager.Domain.Entities;
using EntityManager.Domain.Repositories;
using Mediator;

namespace EntityManager.Application.Queries;

public record GetCompanySuggestionsQuery(string? Name, string? ClientCode, string? ExcludeKeys, int? PageLimit)
    : IRequest<List<Company>>, ICacheableQuery
{
    // Reflection-based - see GetAgentQuery.cs for the full reasoning.
    public string? BuildCacheKey() => CacheKeyBuilder.BuildFromObject(
        ClientCode, "company", "suggest", this, nameof(ClientCode));

    public TimeSpan Ttl => TimeSpan.FromMinutes(10);
    public TimeSpan RefreshWindow => TimeSpan.FromMinutes(2);
}

public class GetCompanySuggestionsQueryHandler : IRequestHandler<GetCompanySuggestionsQuery, List<Company>>
{
    private readonly ICompanyRepository companyRepository;

    public GetCompanySuggestionsQueryHandler(ICompanyRepository companyRepository)
    {
        this.companyRepository = companyRepository;
    }

    public async ValueTask<List<Company>> Handle(GetCompanySuggestionsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new ArgumentException("Name is required");
        if (string.IsNullOrWhiteSpace(request.ClientCode))
            throw new ArgumentException("ClientCode is required");

        return await companyRepository.SuggestCompanies(
            request.Name,
            request.ClientCode,
            SuggestionParams.ParseExcludeKeys(request.ExcludeKeys),
            SuggestionParams.Take(request.PageLimit),
            cancellationToken);
    }
}
