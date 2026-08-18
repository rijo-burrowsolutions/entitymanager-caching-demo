// PURPOSE: matches real ag-kit's file-per-endpoint convention - see
// Agent/MapAgentActionsEndpoint.cs for the full reasoning. MapOfficeUpdateEndpoint
// is NOT called here - see that file's Agent equivalent for why.
namespace EntityManager.Presentation.Endpoints;

using Microsoft.AspNetCore.Routing;

public static class MapOfficeActionsEndpoint
{
    internal static IEndpointRouteBuilder MapOfficeActions(this IEndpointRouteBuilder app)
    {
        app.MapOfficeEndpoints();
        app.MapOfficeListFreshEndpoints();
        app.MapOfficeSuggestEndpoints();
        return app;
    }
}
