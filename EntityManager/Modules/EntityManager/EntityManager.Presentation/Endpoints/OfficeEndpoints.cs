using System.Text.Json;
using EntityManager.Application.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

// PURPOSE: the Presentation layer for Office. Same pattern as
// AgentEndpoints.cs - GET only, hands off to Mediator immediately, and every
// route is wrapped so bad input (e.g. officeKey=0 or a negative key) comes
// back as a clean 400 instead of an unhandled 500.
namespace EntityManager.Presentation.Endpoints;

public static class OfficeEndpoints
{
    public static void MapOfficeEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/Office/get", async (ISender sender, int officeKey, string? clientCode) =>
        {
            try
            {
                var result = await sender.Send(new GetOfficeQuery(officeKey, clientCode));
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
        });

        app.MapGet("/Office/list", async (ISender sender, int? officeKey, string? officeName, int? parentCompany, string? clientID, int pageNumber = 1, int pageSize = 20) =>
        {
            try
            {
                var result = await sender.Send(new GetOfficeListQuery(officeKey, clientID, officeName, parentCompany, pageNumber, pageSize));
                return Results.Ok(result);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { message = "Invalid JSON found in RawJson.", details = ex.Message });
            }
        });

        // Side-by-side comparison endpoint - see GetOfficeListFreshQuery.cs /
        // AgentEndpoints.cs's /Agent/list-fresh for what this demonstrates.
        app.MapGet("/Office/list-fresh", async (ISender sender, string? officeName, int? parentCompany, string? clientID, int pageNumber = 1, int pageSize = 20) =>
        {
            try
            {
                var result = await sender.Send(new GetOfficeListFreshQuery(clientID, officeName, parentCompany, pageNumber, pageSize));
                return Results.Ok(result);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { message = "Invalid JSON found in RawJson.", details = ex.Message });
            }
        });
    }
}
