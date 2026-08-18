// PURPOSE: the REAL ag-kit ICompanyRepository contract.
using EntityManager.Domain.Entities;

namespace EntityManager.Domain.Repositories;

public interface ICompanyRepository
{
    Task<Company> GetCompany(
        int? companyKey, string? clientCode, string? companyName, string? email,
        CancellationToken cancellationToken);
    Task<Company?> UpdateCompany(int companyKey, string clientCode, string? companyName, CancellationToken cancellationToken);
    IQueryable<Company> GetCompanyList(
        int? companyKey,
        string? clientCode,
        string? companyName,
        CancellationToken cancellationToken);
    Task<List<Company>> SuggestCompanies(
        string name,
        string clientCode,
        IReadOnlyCollection<int> excludeKeys,
        int take,
        CancellationToken cancellationToken);
}
