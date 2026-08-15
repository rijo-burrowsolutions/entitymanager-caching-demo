// PURPOSE: creates (or refreshes) a fully local, full-read/write copy of the
// real Agent/Office/Company data on SQL Server LocalDB. Run this whenever
// you want a fresh local sandbox to test writes against - it never writes
// anything back to the real database, only SELECTs from it.
//
// Usage: dotnet run --project Tools/SandboxSetup
using EntityManager.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

// Never hardcode real credentials here - this file is committed to source
// control. Set the REAL_ETY_CONNECTION_STRING environment variable locally
// (never checked in) to the real idc_ety connection string before running this.
var RealConnectionString = Environment.GetEnvironmentVariable("REAL_ETY_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "Set the REAL_ETY_CONNECTION_STRING environment variable to the real idc_ety connection string before running this.");

const string SandboxConnectionString =
    @"Server=(localdb)\MSSQLLocalDB;Database=idc_ety_sandbox;Trusted_Connection=True;TrustServerCertificate=True;";

string[] tables = ["Agent", "Office", "Company"];

Console.WriteLine("[sandbox-setup] Creating idc_ety_sandbox schema on (localdb)\\MSSQLLocalDB ...");

var options = new DbContextOptionsBuilder<EntityManagerDbContext>()
    .UseSqlServer(SandboxConnectionString)
    .Options;

// EnsureCreated builds the schema from the SAME entity model + EF configurations
// the real API uses, so table/column names match the real database exactly -
// no hand-written DDL, no risk of the sandbox schema drifting from what the
// C# code actually expects.
using (var db = new EntityManagerDbContext(options))
{
    await db.Database.EnsureCreatedAsync();
}
Console.WriteLine("[sandbox-setup] Schema ready.");

using var sandboxConn = new SqlConnection(SandboxConnectionString);
await sandboxConn.OpenAsync();

foreach (var table in tables)
{
    Console.WriteLine($"[sandbox-setup] Copying {table} ...");

    // Safe to re-run: clear out any rows from a previous (possibly partial/
    // failed) copy before re-inserting, so retries never hit duplicate-key errors.
    using (var truncateCmd = new SqlCommand($"DELETE FROM {table}", sandboxConn))
        await truncateCmd.ExecuteNonQueryAsync();

    // Only map columns that exist on BOTH sides - Ignore()d properties (e.g.
    // Agent.CreatedBy/LastModifiedBy) mean the sandbox table has fewer
    // columns than a raw "SELECT *" against the real table would return.
    // Real idc_ety's column names are UPPERCASE (e.g. "AGENTKEY") while EF
    // Core's EnsureCreated built the sandbox table using the C# property
    // casing (e.g. "AgentKey") - SQL Server treats these as the same
    // identifier, but SqlBulkCopy's ColumnMappings resolution is exact-match,
    // so the TRUE destination casing has to be looked up and used explicitly.
    var destColumnsByNormalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    using (var cmd = new SqlCommand(
        "SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME = @t", sandboxConn))
    {
        cmd.Parameters.AddWithValue("@t", table);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(0);
            destColumnsByNormalized[name] = name;
        }
    }

    using var realConn = new SqlConnection(RealConnectionString);
    await realConn.OpenAsync();
    using var selectCmd = new SqlCommand($"SELECT * FROM {table}", realConn);
    using var sourceReader = await selectCmd.ExecuteReaderAsync();

    using var bulkCopy = new SqlBulkCopy(sandboxConn, SqlBulkCopyOptions.KeepIdentity, null)
    {
        DestinationTableName = table,
        BulkCopyTimeout = 120,
    };

    var mapped = 0;
    for (var i = 0; i < sourceReader.FieldCount; i++)
    {
        var sourceColumnName = sourceReader.GetName(i);
        if (destColumnsByNormalized.TryGetValue(sourceColumnName, out var destColumnName))
        {
            bulkCopy.ColumnMappings.Add(sourceColumnName, destColumnName);
            mapped++;
        }
    }

    await bulkCopy.WriteToServerAsync(sourceReader);
    Console.WriteLine($"[sandbox-setup] {table}: {mapped} columns mapped, copy complete.");
}

Console.WriteLine("[sandbox-setup] Done. Connect with SSMS/Azure Data Studio to (localdb)\\MSSQLLocalDB, database idc_ety_sandbox, to browse or edit the data.");
