using EntityManager.Presentation.Endpoints;
using Microsoft.AspNetCore.Routing;

// PURPOSE: a single aggregator so Program.cs only has to call one method
// (app.MapEntityManagerEndpoints()) instead of three separate ones.
namespace EntityManager.Presentation;

public static class EntityManagerEndpointsExtensions
{
    public static void MapEntityManagerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapAgentEndpoints();
        app.MapOfficeEndpoints();
        app.MapCompanyEndpoints();
        app.MapInternalCacheEndpoints();
    }
}
