// PURPOSE: the real ag-kit Presentation-Contracts convention (see e.g.
// ClientManager's UpdateClientRequest) - a plain nullable-property POCO bound
// from the request body, kept separate from the Application-layer Command so
// the wire shape can evolve independently of it. No data-annotation
// validation here, matching the codebase-wide convention of validating
// inline in the handler instead (see UpdateAgentCommand.cs).
namespace EntityManager.Presentation.Contracts.Agent;

public class UpdateAgentRequest
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? FullName { get; set; }
    public string? EmailAddress { get; set; }
}
