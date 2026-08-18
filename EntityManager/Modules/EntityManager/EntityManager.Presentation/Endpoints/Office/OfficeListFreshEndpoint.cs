// PURPOSE: matches real ag-kit's file-per-endpoint convention - see
// Agent/AgentListFreshEndpoint.cs for the full reasoning.
namespace EntityManager.Presentation.Endpoints;

using System.Text.Json;
using EntityManager.Application.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

public static class OfficeListFreshEndpoint
{
    public static IEndpointRouteBuilder MapOfficeListFreshEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/list-fresh", Handle)
            .WithName("GetOfficesFresh");

        return app;
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetOfficeListFreshQuery query,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(query, cancellationToken);
            return Results.Ok(result);
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { message = "Invalid JSON found in RawJson.", details = ex.Message });
        }
    }
}
