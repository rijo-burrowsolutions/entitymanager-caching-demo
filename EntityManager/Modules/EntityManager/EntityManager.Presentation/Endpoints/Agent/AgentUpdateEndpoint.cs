// PURPOSE: modeled on the real ag-kit Update convention (see
// ClientManager's UpdateClientEndpoint.cs - MapPut, route-bound key, body-bound
// request, ISender injected directly). Real ag-kit's EntityManager module has
// no real update endpoints at all (GET-only, matching real DB permissions),
// so this is new, sandbox-testing-only functionality - never mapped unless
// "UseSandboxDb" is true (see EntityManagerEndpointsExtensions.cs). It
// replaces the old ad-hoc SandboxTestEndpoints.cs with the same capability,
// shaped like a real ag-kit Command endpoint instead of a one-off test route.
using EntityManager.Application.Commands;
using EntityManager.Presentation.Contracts.Agent;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EntityManager.Presentation.Endpoints;

public static class AgentUpdateEndpoint
{
    internal static IEndpointRouteBuilder MapAgentUpdateEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/update/{AgentKey}", Handle)
            .WithName("UpdateAgent")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> Handle(
        int AgentKey,
        string clientCode,
        UpdateAgentRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new UpdateAgentCommand(
                    AgentKey,
                    clientCode,
                    request.FirstName,
                    request.LastName,
                    request.FullName,
                    request.EmailAddress),
                cancellationToken);

            if (result.Status == 404)
                return Results.NotFound(new { message = $"No agent {AgentKey} for clientCode {clientCode} in the sandbox." });

            return Results.Ok(new { status = result.Status, agentKey = result.AgentKey });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
