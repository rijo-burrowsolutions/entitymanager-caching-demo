// PURPOSE: modeled on the real ag-kit Command/Handler convention (see e.g.
// ClientManager's UpdateClientCommand) - a Command + typed Response + Handler
// in one file, exactly like the real Query pattern this demo already
// follows. Sandbox-only in practice: only ever mapped when "UseSandboxDb" is
// true (see AgentUpdateEndpoint.cs / EntityManagerEndpointsExtensions.cs) -
// real ag-kit's EntityManager module has no real update commands at all
// (Commands/Agent/GetAgentCommand.cs there is an empty placeholder, just a
// Mediator assembly-scan marker), so this is new, sandbox-testing-only
// functionality, not a port of something that already exists in production.
using EntityManager.Domain.Repositories;
using Mediator;

namespace EntityManager.Application.Commands;

public record UpdateAgentCommand(
    int AgentKey,
    string ClientCode,
    string? FirstName,
    string? LastName,
    string? FullName,
    string? EmailAddress
) : IRequest<UpdateAgentResponse>;

public record UpdateAgentResponse(int Status, int AgentKey = 0);

public class UpdateAgentCommandHandler : IRequestHandler<UpdateAgentCommand, UpdateAgentResponse>
{
    private readonly IAgentRepository agentRepository;

    public UpdateAgentCommandHandler(IAgentRepository agentRepository)
    {
        this.agentRepository = agentRepository;
    }

    public async ValueTask<UpdateAgentResponse> Handle(UpdateAgentCommand request, CancellationToken cancellationToken)
    {
        if (request.FirstName is null && request.LastName is null && request.FullName is null && request.EmailAddress is null)
            throw new ArgumentException("Provide at least one of firstName, lastName, fullName, emailAddress.");

        var agent = await agentRepository.UpdateAgent(
            request.AgentKey,
            request.ClientCode,
            request.FirstName,
            request.LastName,
            request.FullName,
            request.EmailAddress,
            cancellationToken);

        if (agent is null)
            return new UpdateAgentResponse(404);

        return new UpdateAgentResponse(200, request.AgentKey);
    }
}
