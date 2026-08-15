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
    int CompanyKey,
    string? ClientCode)
    : IRequest<JsonElement?>, ICacheableQuery
{
    // See GetAgentQuery.cs - no ClientCode means not cacheable for this call.
    public string? BuildCacheKey() => string.IsNullOrWhiteSpace(ClientCode) ? null : CacheKeyBuilder.Build(
        ClientCode, "company", "get",
        new Dictionary<string, string?>
        {
            ["companyKey"] = CompanyKey.ToString()
        });

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
        if (request.CompanyKey <= 0)
            throw new ArgumentException("CompanyKey is required");

        var company = await companyRepository.GetCompany(
            request.CompanyKey,
            request.ClientCode,
            cancellationToken);

        if (company == null || string.IsNullOrWhiteSpace(company.RawJson))
            return null;

        var result = CamelCaseConversion.ConvertToCamelCase(company.RawJson);
        var jsonObject = JsonNode.Parse(result.GetRawText())!.AsObject();
        JsonHelper.AddDefaultPicture(jsonObject, configuration["CDN:Url"]!, "COMPANY", request.ClientCode);
        return JsonSerializer.SerializeToElement(jsonObject);
    }
}
