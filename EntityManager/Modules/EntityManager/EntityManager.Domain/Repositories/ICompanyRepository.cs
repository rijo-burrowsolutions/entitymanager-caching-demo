// PURPOSE: the REAL ag-kit ICompanyRepository contract (Suggest omitted -
// this demo only wires up Get + List, matching its existing endpoint scope).
using EntityManager.Domain.Entities;

namespace EntityManager.Domain.Repositories;

public interface ICompanyRepository
{
    Task<Company> GetCompany(int companyKey, string? clientCode, CancellationToken cancellationToken);
    IQueryable<Company> GetCompanyList(
        int? companyKey,
        string? clientCode,
        string? companyName,
        CancellationToken cancellationToken);
}
