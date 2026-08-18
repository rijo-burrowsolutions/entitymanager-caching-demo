// PURPOSE: matches real ag-kit's file-per-endpoint convention - see
// Agent/AgentSuggestEndpoint.cs for the full reasoning.
using EntityManager.Application.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

public static class CompanySuggestEndpoint
{
    public static IEndpointRouteBuilder MapCompanySuggestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/suggest", Handle)
            .WithName("SuggestCompanies")
            .WithSummary("Typeahead over company names. Required: name, clientCode.");

        return app;
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetCompanySuggestionsQuery query,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(query, cancellationToken);
            return Results.Ok(result.Select(x => new { x.CompanyKey, x.CompanyName }));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
