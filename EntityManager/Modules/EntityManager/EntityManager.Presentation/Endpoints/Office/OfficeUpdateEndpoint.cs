// PURPOSE: the Office equivalent of Agent/AgentUpdateEndpoint.cs - see that
// file for the full reasoning (sandbox-only, real ag-kit Update convention).
using EntityManager.Application.Commands;
using EntityManager.Presentation.Contracts.Office;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EntityManager.Presentation.Endpoints;

public static class OfficeUpdateEndpoint
{
    internal static IEndpointRouteBuilder MapOfficeUpdateEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/update/{OfficeKey}", Handle)
            .WithName("UpdateOffice")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> Handle(
        int OfficeKey,
        string clientCode,
        UpdateOfficeRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new UpdateOfficeCommand(OfficeKey, clientCode, request.OfficeName),
                cancellationToken);

            if (result.Status == 404)
                return Results.NotFound(new { message = $"No office {OfficeKey} for clientCode {clientCode} in the sandbox." });

            return Results.Ok(new { status = result.Status, officeKey = result.OfficeKey });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
