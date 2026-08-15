// PURPOSE: write endpoints that exist ONLY for local sandbox testing - they
// let you simulate "someone changed this record" with one POST call instead
// of hand-writing a JSON_MODIFY SQL script every time. Real ag-kit is
// GET-only (matches real DB permissions) and none of these are ever mapped
// unless "UseSandboxDb": true in appsettings.json (see Program.cs) - it is
// structurally impossible to reach these against real production.
//
// Each also always talks to the IDC_ETY_SANDBOX connection string directly,
// never whatever the generic UseSandboxDb toggle happens to resolve to, as a
// second, independent safety net on top of the registration gate.
//
// Why this exists instead of more hand-written SQL: RawJson's keys are
// PascalCase (FullName, OfficeName, CompanyName, ...) and hand-guessing that
// casing in a JSON_MODIFY path is exactly what caused two real bugs earlier -
// a wrong-case path silently ADDS a new duplicate key instead of updating the
// existing one. PatchRawJsonAsync below finds the real existing key
// case-insensitively at runtime, so that mistake can't happen.
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace EntityManager.Presentation.Endpoints;

public static class SandboxTestEndpoints
{
    public static void MapSandboxTestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/Sandbox/agent/update", async (
            IConfiguration configuration,
            int agentKey,
            string clientCode,
            string? firstName,
            string? lastName,
            string? fullName,
            string? emailAddress) =>
        {
            var fields = NonNullFields(
                ("firstname", firstName), ("lastname", lastName),
                ("fullname", fullName), ("emailaddress", emailAddress));
            if (fields.Count == 0)
                return Results.BadRequest(new { message = "Provide at least one of firstName, lastName, fullName, emailAddress." });

            using var conn = await OpenSandboxConnectionAsync(configuration);
            var (jsonObject, applied, skipped) = await PatchRawJsonAsync(
                conn, "Agent", "AGENTKEY", agentKey, clientCode, fields);
            if (jsonObject is null)
                return Results.NotFound(new { message = $"No agent {agentKey} for clientCode {clientCode} in the sandbox." });

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
            await updateCmd.ExecuteNonQueryAsync();

            return Results.Ok(new { agentKey, clientCode, updatedRawJsonKeys = applied, skipped });
        });

        app.MapPost("/Sandbox/office/update", async (
            IConfiguration configuration,
            int officeKey,
            string clientCode,
            string? officeName) =>
        {
            var fields = NonNullFields(("officename", officeName));
            if (fields.Count == 0)
                return Results.BadRequest(new { message = "Provide officeName." });

            using var conn = await OpenSandboxConnectionAsync(configuration);
            var (jsonObject, applied, skipped) = await PatchRawJsonAsync(
                conn, "Office", "OFFICEKEY", officeKey, clientCode, fields);
            if (jsonObject is null)
                return Results.NotFound(new { message = $"No office {officeKey} for clientCode {clientCode} in the sandbox." });

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
            await updateCmd.ExecuteNonQueryAsync();

            return Results.Ok(new { officeKey, clientCode, updatedRawJsonKeys = applied, skipped });
        });

        app.MapPost("/Sandbox/company/update", async (
            IConfiguration configuration,
            int companyKey,
            string clientCode,
            string? companyName) =>
        {
            var fields = NonNullFields(("companyname", companyName));
            if (fields.Count == 0)
                return Results.BadRequest(new { message = "Provide companyName." });

            using var conn = await OpenSandboxConnectionAsync(configuration);
            var (jsonObject, applied, skipped) = await PatchRawJsonAsync(
                conn, "Company", "COMPANYKEY", companyKey, clientCode, fields);
            if (jsonObject is null)
                return Results.NotFound(new { message = $"No company {companyKey} for clientCode {clientCode} in the sandbox." });

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
            await updateCmd.ExecuteNonQueryAsync();

            return Results.Ok(new { companyKey, clientCode, updatedRawJsonKeys = applied, skipped });
        });
    }

    private static Dictionary<string, string?> NonNullFields(params (string Name, string? Value)[] fields) =>
        fields.Where(f => f.Value is not null).ToDictionary(f => f.Name, f => f.Value);

    private static async Task<SqlConnection> OpenSandboxConnectionAsync(IConfiguration configuration)
    {
        var conn = new SqlConnection(configuration.GetConnectionString("IDC_ETY_SANDBOX"));
        await conn.OpenAsync();
        return conn;
    }

    // Loads RawJson for the given table/key, and for each requested field
    // finds its real, already-existing key case-insensitively and updates it
    // in place - never guesses a path, so it can't create a duplicate key.
    private static async Task<(JsonObject? Json, List<string> Applied, List<string> Skipped)> PatchRawJsonAsync(
        SqlConnection conn, string table, string keyColumn, int keyValue, string clientCode,
        Dictionary<string, string?> fields)
    {
        string? rawJson;
        using (var selectCmd = new SqlCommand(
            $"SELECT RawJson FROM {table} WHERE {keyColumn} = @key AND CLIENTCODE = @clientCode", conn))
        {
            selectCmd.Parameters.AddWithValue("@key", keyValue);
            selectCmd.Parameters.AddWithValue("@clientCode", clientCode);
            rawJson = (string?)await selectCmd.ExecuteScalarAsync();
        }

        var applied = new List<string>();
        var skipped = new List<string>();
        if (rawJson is null)
            return (null, applied, skipped);

        var jsonObject = JsonNode.Parse(rawJson)!.AsObject();
        foreach (var (fieldNameLower, value) in fields)
        {
            var actualKey = jsonObject.Select(p => p.Key)
                .FirstOrDefault(k => string.Equals(k, fieldNameLower, StringComparison.OrdinalIgnoreCase));

            if (actualKey is null)
            {
                skipped.Add(fieldNameLower);
                continue;
            }

            jsonObject[actualKey] = value;
            applied.Add(actualKey);
        }

        return (jsonObject, applied, skipped);
    }
}
