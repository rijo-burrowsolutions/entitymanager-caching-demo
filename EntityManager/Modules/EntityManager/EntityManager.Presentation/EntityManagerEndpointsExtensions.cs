using EntityManager.Presentation.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

// PURPOSE: matches real ag-kit's EntityManagerEndpoints.cs - one route group
// per entity ("/Agent", "/Office", "/Company"), each chain-calling that
// entity's MapXActions() aggregator. The one deliberate deviation: each
// group also conditionally maps that entity's Update endpoint, but ONLY when
// useSandboxDb is true - Update is new, sandbox-testing-only functionality
// that doesn't exist in real ag-kit at all (see AgentUpdateEndpoint.cs), so
// it must stay structurally unreachable against real production, exactly
// like the old SandboxTestEndpoints.cs this replaces.
namespace EntityManager.Presentation;

public static class EntityManagerEndpointsExtensions
{
    public static void MapEntityManagerEndpoints(this IEndpointRouteBuilder app, bool useSandboxDb)
    {
        var agent = app.MapGroup("/Agent").WithTags("Agent");
        agent.MapAgentActions();
        if (useSandboxDb)
            agent.MapAgentUpdateEndpoint();

        var office = app.MapGroup("/Office").WithTags("Office");
        office.MapOfficeActions();
        if (useSandboxDb)
            office.MapOfficeUpdateEndpoint();

        var company = app.MapGroup("/Company").WithTags("Company");
        company.MapCompanyActions();
        if (useSandboxDb)
            company.MapCompanyUpdateEndpoint();

        app.MapInternalCacheEndpoints();
    }
}
