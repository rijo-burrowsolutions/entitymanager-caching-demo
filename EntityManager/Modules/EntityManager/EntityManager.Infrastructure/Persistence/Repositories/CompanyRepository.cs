// PURPOSE: the REAL ag-kit CompanyRepository.
using System.Text.Json.Nodes;
using EntityManager.Domain.Entities;
using EntityManager.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EntityManager.Infrastructure.Persistence.Repositories;

public class CompanyRepository : ICompanyRepository
{
    EntityManagerDbContext dbContext;
    public CompanyRepository(EntityManagerDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Company> GetCompany(
        int? companyKey, string? clientCode, string? companyName, string? email,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Company>().Where(x => !x.IsDeleted);

        if (companyKey.HasValue)
        {
            query = query.Where(x => x.CompanyKey == companyKey.Value);
        }

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            query = query.Where(x => x.ClientCode == clientCode);
        }

        // Extra single-record lookup filters - same AND-narrowing convention
        // as companyKey/clientCode above.
        if (!string.IsNullOrWhiteSpace(companyName))
            query = query.Where(x => x.CompanyName!.Contains(companyName));

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(x => x.EmailAddress1 == email);

        var company = await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return company;
    }

    // Sandbox-only in practice - see AgentRepository.UpdateAgent for the full
    // reasoning behind the raw-SQL + case-insensitive RawJson patch approach.
    public async Task<Company?> UpdateCompany(int companyKey, string clientCode, string? companyName, CancellationToken cancellationToken)
    {
        using var conn = new SqlConnection(dbContext.Database.GetConnectionString());
        await conn.OpenAsync(cancellationToken);

        string? rawJson;
        using (var selectCmd = new SqlCommand(
            "SELECT RawJson FROM Company WHERE COMPANYKEY = @key AND CLIENTCODE = @clientCode", conn))
        {
            selectCmd.Parameters.AddWithValue("@key", companyKey);
            selectCmd.Parameters.AddWithValue("@clientCode", clientCode);
            rawJson = (string?)await selectCmd.ExecuteScalarAsync(cancellationToken);
        }

        if (rawJson is null)
            return null;

        var jsonObject = JsonNode.Parse(rawJson)!.AsObject();
        if (companyName is not null)
        {
            var actualKey = jsonObject.Select(p => p.Key)
                .FirstOrDefault(k => string.Equals(k, "companyname", StringComparison.OrdinalIgnoreCase));
            if (actualKey is not null)
                jsonObject[actualKey] = companyName;
        }

        using var updateCmd = new SqlCommand(
            """
            UPDATE Company SET
                CompanyName = COALESCE(@companyName, CompanyName),
                RawJson = @rawJson,
                LASTMODIFIED = SYSDATETIME()
            WHERE COMPANYKEY = @key AND CLIENTCODE = @clientCode
            """, conn);
        updateCmd.Parameters.AddWithValue("@companyName", (object?)companyName ?? DBNull.Value);
        updateCmd.Parameters.AddWithValue("@rawJson", jsonObject.ToJsonString());
        updateCmd.Parameters.AddWithValue("@key", companyKey);
        updateCmd.Parameters.AddWithValue("@clientCode", clientCode);
        await updateCmd.ExecuteNonQueryAsync(cancellationToken);

        return new Company { CompanyKey = companyKey, ClientCode = clientCode, CompanyName = companyName };
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

    public async Task<List<Company>> SuggestCompanies(
        string name, string clientCode, IReadOnlyCollection<int> excludeKeys, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Company>().Where(x => !x.IsDeleted && x.ClientCode == clientCode && x.CompanyName.Contains(name));

        if (excludeKeys.Count > 0)
            query = query.Where(x => !excludeKeys.Contains(x.CompanyKey));

        return await query
            .AsNoTracking()
            .OrderBy(x => x.CompanyName)
            .Take(take)
            .Select(x => new Company { CompanyKey = x.CompanyKey, CompanyName = x.CompanyName })
            .ToListAsync(cancellationToken);
    }
}
