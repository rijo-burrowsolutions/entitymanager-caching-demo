// PURPOSE: matches real ag-kit's file-per-endpoint convention (see
// ag-kit/Modules/EntityManager/EntityManager.Presentation/Endpoints/Agent/AgentQueryEndpoint.cs)
// - one file, one route. [AsParameters] binds query-string values straight
// onto the GetAgentQuery record by property name, so this file never needs
// to change when a new lookup field (e.g. Email) is added to that record.
using System.Text.Json;
using EntityManager.Application.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace EntityManager.Presentation.Endpoints;

public static class AgentEndpoints
{
    public static IEndpointRouteBuilder MapAgentEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/get", Handle)
            .WithName("GetAgent")
            .WithSummary("Get a single agent by agentKey, seoName, firstName, lastName, fullName or email");

        return app;
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetAgentQuery query,
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
