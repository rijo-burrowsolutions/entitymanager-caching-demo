// PURPOSE: matches real ag-kit's file-per-endpoint convention for the list
// route (see AgentListQueryEndpoint.cs there) - this demo's equivalent maps
// GetAgentListFreshQuery instead of a plain GetAgentListQuery, since the
// whole-page /Agent/list endpoint was removed in favor of the ID-list +
// per-record pattern (see MapAgentActionsEndpoint.cs and the README).
namespace EntityManager.Presentation.Endpoints;

using System.Text.Json;
using EntityManager.Application.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

public static class AgentListFreshEndpoint
{
    public static IEndpointRouteBuilder MapAgentListFreshEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/list-fresh", Handle)
            .WithName("GetAgentsFresh");

        return app;
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetAgentListFreshQuery query,
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
