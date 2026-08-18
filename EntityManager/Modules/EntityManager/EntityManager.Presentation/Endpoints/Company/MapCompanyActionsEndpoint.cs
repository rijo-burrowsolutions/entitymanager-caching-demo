// PURPOSE: matches real ag-kit's file-per-endpoint convention - see
// Agent/MapAgentActionsEndpoint.cs for the full reasoning. MapCompanyUpdateEndpoint
// is NOT called here - see that file's Agent equivalent for why.
namespace EntityManager.Presentation.Endpoints;

using Microsoft.AspNetCore.Routing;

public static class MapCompanyActionsEndpoint
{
    internal static IEndpointRouteBuilder MapCompanyActions(this IEndpointRouteBuilder app)
    {
        app.MapCompanyEndpoints();
        app.MapCompanyListFreshEndpoints();
        app.MapCompanySuggestEndpoints();
        return app;
    }
}
