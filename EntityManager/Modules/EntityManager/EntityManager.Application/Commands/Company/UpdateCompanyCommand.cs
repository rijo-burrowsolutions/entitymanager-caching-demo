// PURPOSE: the Company equivalent of UpdateAgentCommand.cs - see that file
// for the full reasoning (real ag-kit Command/Handler convention, sandbox-only in practice).
using EntityManager.Domain.Repositories;
using Mediator;

namespace EntityManager.Application.Commands;

public record UpdateCompanyCommand(
    int CompanyKey,
    string ClientCode,
    string? CompanyName
) : IRequest<UpdateCompanyResponse>;

public record UpdateCompanyResponse(int Status, int CompanyKey = 0);

public class UpdateCompanyCommandHandler : IRequestHandler<UpdateCompanyCommand, UpdateCompanyResponse>
{
    private readonly ICompanyRepository companyRepository;

    public UpdateCompanyCommandHandler(ICompanyRepository companyRepository)
    {
        this.companyRepository = companyRepository;
    }

    public async ValueTask<UpdateCompanyResponse> Handle(UpdateCompanyCommand request, CancellationToken cancellationToken)
    {
        if (request.CompanyName is null)
            throw new ArgumentException("Provide companyName.");

        var company = await companyRepository.UpdateCompany(
            request.CompanyKey,
            request.ClientCode,
            request.CompanyName,
            cancellationToken);

        if (company is null)
            return new UpdateCompanyResponse(404);

        return new UpdateCompanyResponse(200, request.CompanyKey);
    }
}
