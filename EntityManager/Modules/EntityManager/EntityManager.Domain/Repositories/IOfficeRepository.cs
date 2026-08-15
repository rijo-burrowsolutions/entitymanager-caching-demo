// PURPOSE: the REAL ag-kit IOfficeRepository contract (Suggest omitted -
// this demo only wires up Get + List, matching its existing endpoint scope).
using EntityManager.Domain.Entities;

namespace EntityManager.Domain.Repositories;

public interface IOfficeRepository
{
    Task<Office> GetOffice(int officekey, string? clientCode, CancellationToken cancellationToken);
    IQueryable<Office> GetOfficeList(
        int? officeKey,
        string? clientCode,
        string? officeName,
        int? parentCompany,
        CancellationToken cancellationToken);
}
