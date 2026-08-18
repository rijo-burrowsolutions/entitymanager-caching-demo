// PURPOSE: matches real ag-kit's file-per-endpoint convention (see
// ag-kit/Modules/EntityManager/EntityManager.Presentation/Endpoints/Agent/AgentSuggestEndpoint.cs).
// A differently-shaped Get API for the same entity - typeahead over names
// instead of a single-record lookup by key. See GetAgentSuggestionsQuery.cs
// for the caching reasoning (real ag-kit doesn't cache this query; this demo does).
using EntityManager.Application.Queries;
using Mediator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

public static class AgentSuggestEndpoint
{
    public static IEndpointRouteBuilder MapAgentSuggestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/suggest", Handle)
            .WithName("SuggestAgents")
            .WithSummary("Typeahead over agent names. Required: name, clientCode. isTeam: true = teams only, false = agents only, omitted = both.");

        return app;
    }

    private static async Task<IResult> Handle(
        [AsParameters] GetAgentSuggestionsQuery query,
        [FromServices] ISender sender,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await sender.Send(query, cancellationToken);
            return Results.Ok(result.Select(x => new { x.AgentKey, x.FullName, x.IsTeam }));
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }
    }
}
