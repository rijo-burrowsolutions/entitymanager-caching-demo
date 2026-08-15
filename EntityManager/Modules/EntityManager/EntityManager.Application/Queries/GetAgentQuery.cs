// PURPOSE: the REAL ag-kit GetAgentQuery + handler (copied from
// ag-kit/Modules/EntityManager/EntityManager.Application/Queries/Agent/GetAgentQuery.cs),
// with ICacheableQuery added to the record so CachingPipelineBehavior picks
// it up - that's the ONLY change from the real code. The handler itself
// (RawJson -> camelCase -> merge office/company -> return JsonElement) is
// untouched real business logic, now running against real idc_ety data.
using Ag.Cache;
using EntityManager.Domain.Repositories;
using Mediator;
using System.Text.Json;
using Ag.Util.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace EntityManager.Application.Queries;

public record GetAgentQuery(int? AgentKey, string? SeoName, string? ClientCode)
    : IRequest<JsonElement?>, ICacheableQuery
{
    // No ClientCode -> not cacheable for this call (see ICacheableQuery) -
    // otherwise a request that omits it would cache under a fake scope that
    // SandboxWatcher/watcher.js (which only ever knows the row's REAL
    // ClientCode) could never reconstruct a matching key for to invalidate.
    public string? BuildCacheKey() => string.IsNullOrWhiteSpace(ClientCode) ? null : CacheKeyBuilder.Build(
        ClientCode, "agent", "get",
        new Dictionary<string, string?>
        {
            ["agentKey"] = AgentKey?.ToString(),
            ["seoName"] = SeoName
        });

    // Flat 10 min locally for every cacheable query, get and list alike -
    // real production values differ per query type (60 min / 10 min for get,
    // 10 min / 2 min for list - Complete Guide, Part F).
    public TimeSpan Ttl => TimeSpan.FromMinutes(10);
    public TimeSpan RefreshWindow => TimeSpan.FromMinutes(2);
}

public class GetAgentQueryHandler : IRequestHandler<GetAgentQuery, JsonElement?>
{
    private readonly IAgentRepository agentRepository;
    private readonly IConfiguration configuration;

    public GetAgentQueryHandler(IAgentRepository agentRepository, IConfiguration configuration)
    {
        this.agentRepository = agentRepository;
        this.configuration = configuration;
    }

    public async ValueTask<JsonElement?> Handle(GetAgentQuery request, CancellationToken cancellationToken)
    {
        if (!request.AgentKey.HasValue && string.IsNullOrWhiteSpace(request.SeoName))
            throw new ArgumentException("Either AgentKey or SeoName is required");

        var agent = await agentRepository.GetAgentDetail(
            request.AgentKey,
            request.SeoName,
            request.ClientCode,
            cancellationToken);

        if (agent == null || string.IsNullOrWhiteSpace(agent.RawJson))
            return null;

        var camelCaseJson = CamelCaseConversion.ConvertToCamelCase(agent.RawJson);

        var jsonObject = JsonNode.Parse(camelCaseJson.GetRawText())!.AsObject();
        JsonHelper.AddDefaultPicture(jsonObject, configuration["CDN:Url"]!, "AGENT", request.ClientCode);

        jsonObject["office"] = new JsonObject
        {
            ["officeName"] = agent.OfficeName,
            ["city"] = agent.OfficeCity,
            ["state"] = agent.OfficeState,
            ["country"] = agent.OfficeCountry,
            ["street"] = agent.OfficeStreet,
            ["zipCode"] = agent.OfficeZipcode,
            ["phone"] = agent.OfficePhone
        };

        jsonObject["company"] = new JsonObject
        {
            ["companyName"] = agent.CompanyName
        };

        return JsonSerializer.SerializeToElement(jsonObject);
    }
}
