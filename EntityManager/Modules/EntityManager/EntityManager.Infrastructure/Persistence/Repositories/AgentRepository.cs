// PURPOSE: the REAL ag-kit AgentRepository (copied from ag-kit's
// AgentRepository.cs). GetAgentDetail does the real Agent -> Office -> Company
// join that produces the flattened DTO the API actually returns.
using System.Text.Json.Nodes;
using EntityManager.Domain.Entities;
using EntityManager.Domain.Repositories;
using Microsoft.Data.SqlClient;
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

    public async Task<AgentDetail?> GetAgentDetail(
        int? agentKey, string? seoName, string? clientCode,
        string? firstName, string? lastName, string? fullName, string? email,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!agentKey.HasValue && string.IsNullOrWhiteSpace(seoName)
                && string.IsNullOrWhiteSpace(firstName) && string.IsNullOrWhiteSpace(lastName)
                && string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("At least one of AgentKey, SeoName, FirstName, LastName, FullName or Email is required");

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

            // Extra single-record lookup filters, same AND-narrowing convention
            // as AgentKey/SeoName above - every one supplied narrows the match
            // further, it doesn't widen it.
            if (!string.IsNullOrWhiteSpace(firstName))
                query = query.Where(a => a.GivenName!.Contains(firstName));

            if (!string.IsNullOrWhiteSpace(lastName))
                query = query.Where(a => a.SurName!.Contains(lastName));

            if (!string.IsNullOrWhiteSpace(fullName))
                query = query.Where(a => a.FullName!.Contains(fullName));

            if (!string.IsNullOrWhiteSpace(email))
                query = query.Where(a => a.EmailAddress1 == email);

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

    // Sandbox-only in practice (only ever called when UseSandboxDb routes this
    // repository at idc_ety_sandbox - see AgentUpdateEndpoint.cs) - updates
    // both the normalized columns and the matching keys in RawJson,
    // case-insensitively. RawJson's keys are PascalCase (FullName, ...);
    // hand-guessing that casing in a JSON_MODIFY path is exactly what caused
    // real bugs earlier - a wrong-case path silently ADDS a duplicate key
    // instead of updating the existing one. Finding the real existing key
    // case-insensitively at runtime means that mistake can't happen. Uses a
    // raw SqlCommand (not EF Core's change tracking) so LASTMODIFIED is
    // guaranteed to advance via SYSDATETIME() regardless of entity mapping -
    // that's what SandboxWatcher polls on to detect this as a real change.
    public async Task<Agent?> UpdateAgent(
        int agentKey, string clientCode,
        string? firstName, string? lastName, string? fullName, string? emailAddress,
        CancellationToken cancellationToken)
    {
        using var conn = new SqlConnection(dbContext.Database.GetConnectionString());
        await conn.OpenAsync(cancellationToken);

        string? rawJson;
        using (var selectCmd = new SqlCommand(
            "SELECT RawJson FROM Agent WHERE AGENTKEY = @key AND CLIENTCODE = @clientCode", conn))
        {
            selectCmd.Parameters.AddWithValue("@key", agentKey);
            selectCmd.Parameters.AddWithValue("@clientCode", clientCode);
            rawJson = (string?)await selectCmd.ExecuteScalarAsync(cancellationToken);
        }

        if (rawJson is null)
            return null;

        var jsonObject = JsonNode.Parse(rawJson)!.AsObject();
        PatchRawJsonField(jsonObject, "firstname", firstName);
        PatchRawJsonField(jsonObject, "lastname", lastName);
        PatchRawJsonField(jsonObject, "fullname", fullName);
        PatchRawJsonField(jsonObject, "emailaddress", emailAddress);

        using var updateCmd = new SqlCommand(
            """
            UPDATE Agent SET
                GivenName = COALESCE(@firstName, GivenName),
                SurName = COALESCE(@lastName, SurName),
                FullName = COALESCE(@fullName, FullName),
                EmailAddress1 = COALESCE(@emailAddress, EmailAddress1),
                RawJson = @rawJson,
                LASTMODIFIED = SYSDATETIME()
            WHERE AGENTKEY = @key AND CLIENTCODE = @clientCode
            """, conn);
        updateCmd.Parameters.AddWithValue("@firstName", (object?)firstName ?? DBNull.Value);
        updateCmd.Parameters.AddWithValue("@lastName", (object?)lastName ?? DBNull.Value);
        updateCmd.Parameters.AddWithValue("@fullName", (object?)fullName ?? DBNull.Value);
        updateCmd.Parameters.AddWithValue("@emailAddress", (object?)emailAddress ?? DBNull.Value);
        updateCmd.Parameters.AddWithValue("@rawJson", jsonObject.ToJsonString());
        updateCmd.Parameters.AddWithValue("@key", agentKey);
        updateCmd.Parameters.AddWithValue("@clientCode", clientCode);
        await updateCmd.ExecuteNonQueryAsync(cancellationToken);

        return new Agent { AgentKey = agentKey, ClientCode = clientCode, FullName = fullName ?? "" };
    }

    // Finds the field's real, already-existing key case-insensitively and
    // updates it in place - never guesses a path, so it can't create a
    // duplicate key. No-op if the value wasn't supplied or the key doesn't exist.
    private static void PatchRawJsonField(JsonObject jsonObject, string fieldNameLower, string? value)
    {
        if (value is null)
            return;

        var actualKey = jsonObject.Select(p => p.Key)
            .FirstOrDefault(k => string.Equals(k, fieldNameLower, StringComparison.OrdinalIgnoreCase));

        if (actualKey is not null)
            jsonObject[actualKey] = value;
    }

    public async Task<List<Agent>> SuggestAgents(
        string name, string clientCode, bool? isTeam,
        IReadOnlyCollection<int> excludeKeys, int take, CancellationToken cancellationToken)
    {
        var query = dbContext.Set<Agent>()
            .Where(x => !x.IsDeleted && x.ClientCode == clientCode && x.FullName.Contains(name));

        if (isTeam.HasValue)
            query = query.Where(x => x.IsTeam == isTeam.Value);

        if (excludeKeys.Count > 0)
            query = query.Where(x => !excludeKeys.Contains(x.AgentKey));

        return await query
            .AsNoTracking()
            .OrderBy(x => x.FullName)
            .Take(take)
            .Select(x => new Agent { AgentKey = x.AgentKey, FullName = x.FullName, IsTeam = x.IsTeam })
            .ToListAsync(cancellationToken);
    }
}
