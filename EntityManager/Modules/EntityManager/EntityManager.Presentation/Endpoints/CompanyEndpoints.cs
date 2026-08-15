using System.Text.Json;
using EntityManager.Application.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

// PURPOSE: the Presentation layer for Company. Same pattern as
// AgentEndpoints.cs - GET only, hands off to Mediator immediately, and every
// route is wrapped so bad input (e.g. companyKey=0 or a negative key) comes
// back as a clean 400 instead of an unhandled 500.
namespace EntityManager.Presentation.Endpoints;

public static class CompanyEndpoints
{
    public static void MapCompanyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/Company/get", async (ISender sender, int companyKey, string? clientCode) =>
        {
            try
            {
                var result = await sender.Send(new GetCompanyQuery(companyKey, clientCode));
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

        app.MapGet("/Company/list", async (ISender sender, int? companyKey, string? companyName, string? clientID, int pageNumber = 1, int pageSize = 20) =>
        {
            try
            {
                var result = await sender.Send(new GetCompanyListQuery(companyKey, clientID, companyName, pageNumber, pageSize));
                return Results.Ok(result);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { message = "Invalid JSON found in RawJson.", details = ex.Message });
            }
        });

        // Side-by-side comparison endpoint - see GetCompanyListFreshQuery.cs /
        // AgentEndpoints.cs's /Agent/list-fresh for what this demonstrates.
        app.MapGet("/Company/list-fresh", async (ISender sender, string? companyName, string? clientID, int pageNumber = 1, int pageSize = 20) =>
        {
            try
            {
                var result = await sender.Send(new GetCompanyListFreshQuery(clientID, companyName, pageNumber, pageSize));
                return Results.Ok(result);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { message = "Invalid JSON found in RawJson.", details = ex.Message });
            }
        });
    }
}
