// PURPOSE: the Office equivalent of UpdateAgentCommand.cs - see that file for
// the full reasoning (real ag-kit Command/Handler convention, sandbox-only in practice).
using EntityManager.Domain.Repositories;
using Mediator;

namespace EntityManager.Application.Commands;

public record UpdateOfficeCommand(
    int OfficeKey,
    string ClientCode,
    string? OfficeName
) : IRequest<UpdateOfficeResponse>;

public record UpdateOfficeResponse(int Status, int OfficeKey = 0);

public class UpdateOfficeCommandHandler : IRequestHandler<UpdateOfficeCommand, UpdateOfficeResponse>
{
    private readonly IOfficeRepository officeRepository;

    public UpdateOfficeCommandHandler(IOfficeRepository officeRepository)
    {
        this.officeRepository = officeRepository;
    }

    public async ValueTask<UpdateOfficeResponse> Handle(UpdateOfficeCommand request, CancellationToken cancellationToken)
    {
        if (request.OfficeName is null)
            throw new ArgumentException("Provide officeName.");

        var office = await officeRepository.UpdateOffice(
            request.OfficeKey,
            request.ClientCode,
            request.OfficeName,
            cancellationToken);

        if (office is null)
            return new UpdateOfficeResponse(404);

        return new UpdateOfficeResponse(200, request.OfficeKey);
    }
}
