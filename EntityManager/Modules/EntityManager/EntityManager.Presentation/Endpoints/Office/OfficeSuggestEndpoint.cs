// PURPOSE: matches real ag-kit's file-per-endpoint convention - see
// Agent/AgentSuggestEndpoint.cs for the full reasoning.
using EntityManager.Application.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

public static class OfficeSuggestEndpoint
{
    public static IEndpointRouteBuilder MapOfficeSuggestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/suggest", Handle)
            .WithName("SuggestOffices")
            .WithSummary("Typeahead over office names. Required: name, clientCode.");

        return app;
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetOfficeSuggestionsQuery query,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(query, cancellationToken);
            return Results.Ok(result.Select(x => new { x.OfficeKey, x.OfficeName }));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
