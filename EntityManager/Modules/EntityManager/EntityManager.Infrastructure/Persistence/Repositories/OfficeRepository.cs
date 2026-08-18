// PURPOSE: the REAL ag-kit OfficeRepository.
using System.Text.Json.Nodes;
using EntityManager.Domain.Entities;
using EntityManager.Domain.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EntityManager.Infrastructure.Persistence.Repositories;

public class OfficeRepository : IOfficeRepository
{
    EntityManagerDbContext dbContext;
    public OfficeRepository(EntityManagerDbContext dbContext)
    {
        this.dbContext = dbContext;
    }

    public async Task<Office> GetOffice(
        int? officekey, string? clientCode, string? officeName, string? city, string? email,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Office>().Where(x => !x.IsDeleted);

        if (officekey.HasValue)
        {
            query = query.Where(x => x.OfficeKey == officekey.Value);
        }

        if (!string.IsNullOrWhiteSpace(clientCode))
        {
            query = query.Where(x => x.ClientCode == clientCode);
        }

        // Extra single-record lookup filters - same AND-narrowing convention
        // as officekey/clientCode above.
        if (!string.IsNullOrWhiteSpace(officeName))
            query = query.Where(x => x.OfficeName!.Contains(officeName));

        if (!string.IsNullOrWhiteSpace(city))
            query = query.Where(x => x.City!.Contains(city));

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(x => x.EmailAddress1 == email);

        var office = await query.AsNoTracking().FirstOrDefaultAsync(cancellationToken);
        return office;
    }

    // Sandbox-only in practice - see AgentRepository.UpdateAgent for the full
    // reasoning behind the raw-SQL + case-insensitive RawJson patch approach.
    public async Task<Office?> UpdateOffice(int officeKey, string clientCode, string? officeName, CancellationToken cancellationToken)
    {
        using var conn = new SqlConnection(dbContext.Database.GetConnectionString());
        await conn.OpenAsync(cancellationToken);

        string? rawJson;
        using (var selectCmd = new SqlCommand(
            "SELECT RawJson FROM Office WHERE OFFICEKEY = @key AND CLIENTCODE = @clientCode", conn))
        {
            selectCmd.Parameters.AddWithValue("@key", officeKey);
            selectCmd.Parameters.AddWithValue("@clientCode", clientCode);
            rawJson = (string?)await selectCmd.ExecuteScalarAsync(cancellationToken);
        }

        if (rawJson is null)
            return null;

        var jsonObject = JsonNode.Parse(rawJson)!.AsObject();
        if (officeName is not null)
        {
            var actualKey = jsonObject.Select(p => p.Key)
                .FirstOrDefault(k => string.Equals(k, "officename", StringComparison.OrdinalIgnoreCase));
            if (actualKey is not null)
                jsonObject[actualKey] = officeName;
        }

        using var updateCmd = new SqlCommand(
            """
            UPDATE Office SET
                OfficeName = COALESCE(@officeName, OfficeName),
                RawJson = @rawJson,
                LASTMODIFIED = SYSDATETIME()
            WHERE OFFICEKEY = @key AND CLIENTCODE = @clientCode
            """, conn);
        updateCmd.Parameters.AddWithValue("@officeName", (object?)officeName ?? DBNull.Value);
        updateCmd.Parameters.AddWithValue("@rawJson", jsonObject.ToJsonString());
        updateCmd.Parameters.AddWithValue("@key", officeKey);
        updateCmd.Parameters.AddWithValue("@clientCode", clientCode);
        await updateCmd.ExecuteNonQueryAsync(cancellationToken);

        return new Office { OfficeKey = officeKey, ClientCode = clientCode, OfficeName = officeName };
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

    public async Task<List<Office>> SuggestOffices(
        string name, string clientCode, IReadOnlyCollection<int> excludeKeys, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Office>().Where(x => !x.IsDeleted && x.ClientCode == clientCode && x.OfficeName.Contains(name));

        if (excludeKeys.Count > 0)
            query = query.Where(x => !excludeKeys.Contains(x.OfficeKey));

        return await query
            .AsNoTracking()
            .OrderBy(x => x.OfficeName)
            .Take(take)
            .Select(x => new Office { OfficeKey = x.OfficeKey, OfficeName = x.OfficeName })
            .ToListAsync(cancellationToken);
    }
}
