// PURPOSE: the REAL ag-kit AgentRepository (copied from ag-kit's
// AgentRepository.cs, minus SuggestAgents - this demo only wires up
// Get + List). GetAgentDetail does the real Agent -> Office -> Company join
// that produces the flattened DTO the API actually returns.
using EntityManager.Domain.Entities;
using EntityManager.Domain.Repositories;
using Microsoft.EntityFrameworkCore;

namespace EntityManager.Infrastructure.Persistence.Repositories;

public class AgentRepository : IAgentRepository
{
    EntityManagerDbContext dbContext;
    public AgentRepository(EntityManagerDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Agent> GetAgent(int? agentKey, string? seoName, string? clientCode, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Agent>().Where(x => !x.IsDeleted);

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            query = query.Where(x => x.ClientCode == clientCode);
        }

        if (agentKey.HasValue)
        {
            query = query.Where(x => x.AgentKey == agentKey.Value);
        }

        if (!string.IsNullOrWhiteSpace(seoName))
        {
            if (!seoName.StartsWith("/"))
            {
                seoName = "/" + seoName;
            }

            query = query.Where(x => x.SeoName == seoName);
        }

        var agent = await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return agent;
    }

    public async Task<AgentDetail?> GetAgentDetail(int? agentKey, string? seoName, string? clientCode, CancellationToken cancellationToken)
    {
        try
        {
            if (!agentKey.HasValue && string.IsNullOrWhiteSpace(seoName))
                throw new ArgumentException("Either AgentKey or SeoName is required");

            var query = dbContext.Set<Agent>()
                .AsNoTracking()
                .Where(a => !a.IsDeleted && a.IsDisplayedOnWebsite);

            if (agentKey.HasValue)
            {
                query = query.Where(a => a.AgentKey == agentKey.Value);
            }

            if (!string.IsNullOrWhiteSpace(seoName))
            {
                if (!seoName.StartsWith("/"))
                    seoName = "/" + seoName;

                query = query.Where(a => a.SeoName == seoName);
            }

            if (!string.IsNullOrWhiteSpace(clientCode))
            {
                query = query.Where(a => a.ClientCode == clientCode);
            }

            var result = await query
                .Join(
                    dbContext.Set<Office>().Where(o => !o.IsDeleted),
                    a => a.ParentOffice,
                    o => o.OfficeKey,
                    (a, o) => new { a, o }
                )
                .Join(
                    dbContext.Set<Company>().Where(c => !c.IsDeleted),
                    x => x.a.LinkCompany,
                    c => c.CompanyKey,
                    (x, c) => new AgentDetail
                    {
                        RawJson = x.a.RawJson,

                        OfficeName = x.o.OfficeName,
                        OfficeCity = x.o.City,
                        OfficeCountry = x.o.Country,
                        OfficePhone = x.o.Phone,
                        OfficeState = x.o.State,
                        OfficeStreet = x.o.Street,
                        OfficeZipcode = x.o.ZipCode,

                        CompanyName = c.CompanyName
                    })
                .FirstOrDefaultAsync(cancellationToken);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new Exception("Unable to retrieve agent details.", ex);
        }
    }

    public IQueryable<Agent> GetAgentList(
        int? agentKey,
        string? clientCode,
        string? fullName,
        string? seoName,
        CancellationToken cancellationToken)
    {
        // Matches GetAgentDetail's effective filtering (IsDisplayedOnWebsite +
        // a resolvable, non-deleted office/company) so every AgentKey this
        // list ever returns is guaranteed fetchable via GetAgentDetail too -
        // otherwise a list/id-list result can include agents that 404 when
        // looked up individually (e.g. by GetAgentListFreshQuery's per-record
        // fan-out), silently returning fewer than PageSize items.
        var query = this.dbContext.Set<Agent>()
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsDisplayedOnWebsite)
            .Where(x => dbContext.Set<Office>().Any(o => !o.IsDeleted && o.OfficeKey == x.ParentOffice))
            .Where(x => dbContext.Set<Company>().Any(c => !c.IsDeleted && c.CompanyKey == x.LinkCompany));

        if (agentKey.HasValue)
        {
            query = query.Where(x => x.AgentKey == agentKey.Value);
        }

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            query = query.Where(x => x.ClientCode == clientCode);
        }

        if (!string.IsNullOrWhiteSpace(fullName))
        {
            query = query.Where(x => x.FullName.Contains(fullName));
        }

        if (!string.IsNullOrWhiteSpace(seoName))
        {
            if (!seoName.StartsWith("/"))
            {
                seoName = "/" + seoName;
            }

            query = query.Where(x => x.SeoName == seoName);
        }
        return query.OrderBy(x => x.FullName);
    }
}
