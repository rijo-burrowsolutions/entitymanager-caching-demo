// PURPOSE: the Company equivalent of Agent/AgentUpdateEndpoint.cs - see that
// file for the full reasoning (sandbox-only, real ag-kit Update convention).
using EntityManager.Application.Commands;
using EntityManager.Presentation.Contracts.Company;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace EntityManager.Presentation.Endpoints;

public static class CompanyUpdateEndpoint
{
    internal static IEndpointRouteBuilder MapCompanyUpdateEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPut("/update/{CompanyKey}", Handle)
            .WithName("UpdateCompany")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        return app;
    }

    private static async Task<IResult> Handle(
        int CompanyKey,
        string clientCode,
        UpdateCompanyRequest request,
        ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(
                new UpdateCompanyCommand(CompanyKey, clientCode, request.CompanyName),
                cancellationToken);

            if (result.Status == 404)
                return Results.NotFound(new { message = $"No company {CompanyKey} for clientCode {clientCode} in the sandbox." });

            return Results.Ok(new { status = result.Status, companyKey = result.CompanyKey });
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
