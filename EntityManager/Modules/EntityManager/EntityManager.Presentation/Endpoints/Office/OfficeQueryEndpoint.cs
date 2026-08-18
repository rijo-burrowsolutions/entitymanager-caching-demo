// PURPOSE: matches real ag-kit's file-per-endpoint convention - see
// Agent/AgentQueryEndpoint.cs for the full reasoning.
using System.Text.Json;
using EntityManager.Application.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EntityManager.Presentation.Endpoints;

public static class OfficeEndpoints
{
    public static IEndpointRouteBuilder MapOfficeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/get", Handle)
            .WithName("GetOffice")
            .WithSummary("Get a single office by officeKey, officeName, city or email");

        return app;
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetOfficeQuery query,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(query, cancellationToken);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
        catch (JsonException ex)
        {
            return Results.BadRequest(new { message = "Invalid JSON found in RawJson.", details = ex.Message });
        }
    }
}
