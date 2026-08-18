// PURPOSE: the REAL ag-kit GetCompanyQuery + handler, with ICacheableQuery added.
using EntityManager.Domain.Repositories;
using Mediator;
using System.Text.Json;
using Ag.Util.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Ag.Cache;

namespace EntityManager.Application.Queries;

public record GetCompanyQuery(
    int? CompanyKey,
    string? ClientCode,
    string? CompanyName = null, string? Email = null)
    : IRequest<JsonElement?>, ICacheableQuery
{
    // Reflection-based - see GetAgentQuery.cs for the full reasoning.
    public string? BuildCacheKey() => CacheKeyBuilder.BuildFromObject(
        ClientCode, "company", "get", this, nameof(ClientCode));

    // Flat 10 min locally for every cacheable query - see GetAgentQuery.cs.
    public TimeSpan Ttl => TimeSpan.FromMinutes(10);
    public TimeSpan RefreshWindow => TimeSpan.FromMinutes(2);
}

public class GetCompanyQueryHandler
    : IRequestHandler<GetCompanyQuery, JsonElement?>
{
    private readonly ICompanyRepository companyRepository;
    private readonly IConfiguration configuration;

    public GetCompanyQueryHandler(
        ICompanyRepository companyRepository, IConfiguration configuration)
    {
        this.companyRepository = companyRepository;
        this.configuration = configuration;
    }

    public async ValueTask<JsonElement?> Handle(
        GetCompanyQuery request,
        CancellationToken cancellationToken)
    {
        if (!request.CompanyKey.HasValue && string.IsNullOrWhiteSpace(request.CompanyName)
            && string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("At least one of CompanyKey, CompanyName or Email is required");

        if (request.CompanyKey is <= 0)
            throw new ArgumentException("CompanyKey must be a positive number");

        var company = await companyRepository.GetCompany(
            request.CompanyKey,
            request.ClientCode,
            request.CompanyName,
            request.Email,
            cancellationToken);

        if (company == null || string.IsNullOrWhiteSpace(company.RawJson))
            return null;

        var result = CamelCaseConversion.ConvertToCamelCase(company.RawJson);
        var jsonObject = JsonNode.Parse(result.GetRawText())!.AsObject();
        JsonHelper.AddDefaultPicture(jsonObject, configuration["CDN:Url"]!, "COMPANY", request.ClientCode);
        return JsonSerializer.SerializeToElement(jsonObject);
    }
}
