// PURPOSE: matches real ag-kit's file-per-endpoint convention (see
// ag-kit/Modules/EntityManager/EntityManager.Presentation/Endpoints/Agent/MapAgentActionsEndpoint.cs)
// - one aggregator per entity so EntityManagerEndpointsExtensions.cs only has
// to call one method per entity group. MapAgentUpdateEndpoint is NOT called
// here - it's mapped separately, only when UseSandboxDb is true (see
// EntityManagerEndpointsExtensions.cs), since it's sandbox-only write
// functionality that must never be reachable against real production.
namespace EntityManager.Presentation.Endpoints;

using Microsoft.AspNetCore.Routing;

public static class MapAgentActionsEndpoint
{
    internal static IEndpointRouteBuilder MapAgentActions(this IEndpointRouteBuilder app)
    {
        app.MapAgentEndpoints();
        app.MapAgentListFreshEndpoints();
        app.MapAgentSuggestEndpoints();
        return app;
    }
}
