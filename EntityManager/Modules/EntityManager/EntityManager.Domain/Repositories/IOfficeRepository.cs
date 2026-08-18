// PURPOSE: the REAL ag-kit IOfficeRepository contract.
using EntityManager.Domain.Entities;

namespace EntityManager.Domain.Repositories;

public interface IOfficeRepository
{
    Task<Office> GetOffice(
        int? officekey, string? clientCode, string? officeName, string? city, string? email,
        CancellationToken cancellationToken);
    Task<Office?> UpdateOffice(int officeKey, string clientCode, string? officeName, CancellationToken cancellationToken);
    IQueryable<Office> GetOfficeList(
        int? officeKey,
        string? clientCode,
        string? officeName,
        int? parentCompany,
        CancellationToken cancellationToken);
    Task<List<Office>> SuggestOffices(
        string name,
        string clientCode,
        IReadOnlyCollection<int> excludeKeys,
        int take,
        CancellationToken cancellationToken);
}
