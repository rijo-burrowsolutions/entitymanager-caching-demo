// PURPOSE: the REAL ag-kit GetOfficeQuery + handler, with ICacheableQuery added.
using EntityManager.Domain.Repositories;
using Mediator;
using System.Text.Json;
using Ag.Util.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Ag.Cache;

namespace EntityManager.Application.Queries;

public record GetOfficeQuery(
    int OfficeKey,
    string? ClientCode)
    : IRequest<JsonElement?>, ICacheableQuery
{
    // See GetAgentQuery.cs - no ClientCode means not cacheable for this call.
    public string? BuildCacheKey() => string.IsNullOrWhiteSpace(ClientCode) ? null : CacheKeyBuilder.Build(
        ClientCode, "office", "get",
        new Dictionary<string, string?>
        {
            ["officeKey"] = OfficeKey.ToString()
        });

    // Flat 10 min locally for every cacheable query - see GetAgentQuery.cs.
    public TimeSpan Ttl => TimeSpan.FromMinutes(10);
    public TimeSpan RefreshWindow => TimeSpan.FromMinutes(2);
}

public class GetOfficeQueryHandler
    : IRequestHandler<GetOfficeQuery, JsonElement?>
{
    private readonly IOfficeRepository officeRepository;
    private readonly IConfiguration configuration;

    public GetOfficeQueryHandler(
        IOfficeRepository officeRepository, IConfiguration configuration)
    {
        this.officeRepository = officeRepository;
        this.configuration = configuration;
    }

    public async ValueTask<JsonElement?> Handle(
        GetOfficeQuery request,
        CancellationToken cancellationToken)
    {
        if (request.OfficeKey <= 0)
            throw new ArgumentException("OfficeKey is required");

        var office = await officeRepository.GetOffice(
            request.OfficeKey,
            request.ClientCode,
            cancellationToken);

        if (office == null || string.IsNullOrWhiteSpace(office.RawJson))
            return null;

        var result = CamelCaseConversion.ConvertToCamelCase(office.RawJson);
        var jsonObject = JsonNode.Parse(result.GetRawText())!.AsObject();
        JsonHelper.AddDefaultPicture(jsonObject, configuration["CDN:Url"]!, "OFFICE", request.ClientCode);
        return JsonSerializer.SerializeToElement(jsonObject);
    }
}
