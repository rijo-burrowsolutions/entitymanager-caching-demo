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
    int? OfficeKey,
    string? ClientCode,
    string? OfficeName = null, string? City = null, string? Email = null)
    : IRequest<JsonElement?>, ICacheableQuery
{
    // Reflection-based - see GetAgentQuery.cs for the full reasoning.
    public string? BuildCacheKey() => CacheKeyBuilder.BuildFromObject(
        ClientCode, "office", "get", this, nameof(ClientCode));

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
        if (!request.OfficeKey.HasValue && string.IsNullOrWhiteSpace(request.OfficeName)
            && string.IsNullOrWhiteSpace(request.City) && string.IsNullOrWhiteSpace(request.Email))
            throw new ArgumentException("At least one of OfficeKey, OfficeName, City or Email is required");

        if (request.OfficeKey is <= 0)
            throw new ArgumentException("OfficeKey must be a positive number");

        var office = await officeRepository.GetOffice(
            request.OfficeKey,
            request.ClientCode,
            request.OfficeName,
            request.City,
            request.Email,
            cancellationToken);

        if (office == null || string.IsNullOrWhiteSpace(office.RawJson))
            return null;

        var result = CamelCaseConversion.ConvertToCamelCase(office.RawJson);
        var jsonObject = JsonNode.Parse(result.GetRawText())!.AsObject();
        JsonHelper.AddDefaultPicture(jsonObject, configuration["CDN:Url"]!, "OFFICE", request.ClientCode);
        return JsonSerializer.SerializeToElement(jsonObject);
    }
}
