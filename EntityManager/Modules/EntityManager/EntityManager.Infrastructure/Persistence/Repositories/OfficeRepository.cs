// PURPOSE: the REAL ag-kit OfficeRepository (copied from ag-kit's
// OfficeRepository.cs, minus SuggestOffices - out of scope for this demo).
using EntityManager.Domain.Entities;
using EntityManager.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EntityManager.Infrastructure.Persistence.Repositories;

public class OfficeRepository : IOfficeRepository
{
    EntityManagerDbContext dbContext;
    public OfficeRepository(EntityManagerDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Office> GetOffice(int officekey, string? clientCode, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Office>().Where(x => !x.IsDeleted && x.OfficeKey == officekey);

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            query = query.Where(x => x.ClientCode == clientCode);
        }

        var office = await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return office;
    }

    public IQueryable<Office> GetOfficeList(
        int? officeKey,
        string? clientCode,
        string? officeName,
        int? parentCompany,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Office>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted);

        if (officeKey.HasValue)
        {
            query = query.Where(x => x.OfficeKey == officeKey.Value);
        }

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            query = query.Where(x => x.ClientCode == clientCode);
        }

        if (!string.IsNullOrWhiteSpace(officeName))
        {
            query = query.Where(x => x.OfficeName.Contains(officeName));
        }

        if (parentCompany.HasValue)
        {
            query = query.Where(x => x.ParentCompany == parentCompany.Value);
        }

        return query
            .OrderBy(x => x.OfficeName)
            .ThenBy(x => x.OfficeKey);
    }
}
