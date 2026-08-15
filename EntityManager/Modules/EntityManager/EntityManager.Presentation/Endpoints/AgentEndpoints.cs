using System.Text.Json;
using EntityManager.Application.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

// PURPOSE: the Presentation layer for Agent - the ONLY code that knows about
// HTTP. Reads query-string parameters, builds the matching Query object,
// hands it to Mediator (sender.Send), and shapes the HTTP response. GET only
// - no POST/PUT/DELETE routes exist here. Every route below is wrapped so
// bad input (e.g. no agentKey AND no seoName) comes back as a clean 400
// instead of an unhandled 500.
namespace EntityManager.Presentation.Endpoints;

public static class AgentEndpoints
{
    public static void MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/Agent/get", async (ISender sender, int? agentKey, string? seoName, string? clientCode) =>
        {
            try
            {
                var result = await sender.Send(new GetAgentQuery(agentKey, seoName, clientCode));
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

        app.MapGet("/Agent/list", async (ISender sender, int? agentKey, string? fullName, string? seoName, string? clientID, int pageNumber = 1, int pageSize = 20) =>
        {
            try
            {
                var result = await sender.Send(new GetAgentListQuery(agentKey, clientID, fullName, seoName, pageNumber, pageSize));
                return Results.Ok(result);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { message = "Invalid JSON found in RawJson.", details = ex.Message });
            }
        });

        // Side-by-side comparison endpoint: same filters/shape as /Agent/list,
        // but built from an ID-list cache + per-record caches instead of one
        // big cached page - see GetAgentListFreshQuery.cs for why that means
        // an updated agent is never served stale here, unlike /Agent/list.
        app.MapGet("/Agent/list-fresh", async (ISender sender, string? fullName, string? seoName, string? clientID, int pageNumber = 1, int pageSize = 20) =>
        {
            try
            {
                var result = await sender.Send(new GetAgentListFreshQuery(clientID, fullName, seoName, pageNumber, pageSize));
                return Results.Ok(result);
            }
            catch (JsonException ex)
            {
                return Results.BadRequest(new { message = "Invalid JSON found in RawJson.", details = ex.Message });
            }
        });
    }
}
