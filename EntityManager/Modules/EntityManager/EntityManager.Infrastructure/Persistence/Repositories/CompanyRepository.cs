// PURPOSE: the REAL ag-kit CompanyRepository (copied from ag-kit's
// CompanyRepository.cs, minus SuggestCompanies - out of scope for this demo).
using EntityManager.Domain.Entities;
using EntityManager.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EntityManager.Infrastructure.Persistence.Repositories;

public class CompanyRepository : ICompanyRepository
{
    EntityManagerDbContext dbContext;
    public CompanyRepository(EntityManagerDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Company> GetCompany(int companyKey, string? clientCode, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Company>().Where(x => !x.IsDeleted && x.CompanyKey == companyKey);

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            query = query.Where(x => x.ClientCode == clientCode);
        }

        var company = await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return company;
    }

    public IQueryable<Company> GetCompanyList(
        int? companyKey,
        string? clientCode,
        string? companyName,
        CancellationToken cancellationToken)
    {
        var query = this.dbContext.Set<Company>().AsNoTracking().Where(x => !x.IsDeleted);

        if (companyKey.HasValue)
        {
            query = query.Where(x => x.CompanyKey == companyKey.Value);
        }

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            query = query.Where(x => x.ClientCode == clientCode);
        }

        if (!string.IsNullOrWhiteSpace(companyName))
        {
            query = query.Where(x => x.CompanyName.Contains(companyName));
        }

        return query
            .OrderBy(x => x.CompanyName)
            .ThenBy(x => x.CompanyKey);
    }
}
