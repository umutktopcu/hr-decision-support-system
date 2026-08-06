using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace HrDecisionSupport.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class PostgreSqlIntegrationSmokeTests(PostgreSqlIntegrationTestFixture fixture)
{
    [PostgreSqlIntegrationFact]
    public async Task Fixture_ConnectsMigratesAndUsesNpgsql()
    {
        fixture.RequireConfigured();
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
        Assert.NotEmpty(await context.Database.GetAppliedMigrationsAsync());
    }

    [PostgreSqlIntegrationFact]
    public async Task Context_PersistsDataAcrossFreshDbContext_AndResetPreservesMigrationHistory()
    {
        fixture.RequireConfigured();
        await fixture.ResetDatabaseAsync();
        var migrations = await fixture.GetMigrationHistoryCountAsync();
        await using (var context = fixture.CreateDbContext())
        {
            context.EmployeeImportBatches.Add(Batch("a"));
            await context.SaveChangesAsync();
        }
        await using (var context = fixture.CreateDbContext()) Assert.Single(await context.EmployeeImportBatches.ToListAsync());
        await fixture.ResetDatabaseAsync();
        await using (var context = fixture.CreateDbContext()) Assert.Empty(await context.EmployeeImportBatches.ToListAsync());
        Assert.Equal(migrations, await fixture.GetMigrationHistoryCountAsync());
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportRow_JsonbRoundTripsAndRejectsInvalidJson()
    {
        fixture.RequireConfigured();
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        var batch = Batch("b"); context.EmployeeImportBatches.Add(batch); await context.SaveChangesAsync();
        context.EmployeeImportRows.Add(Row(batch.Id, 2, "EMP-1", "{\"source\":\"smoke\"}", "[{\"code\":\"warning\"}]")); await context.SaveChangesAsync();
        var saved = await context.EmployeeImportRows.SingleAsync(); Assert.Equal("{\"source\":\"smoke\"}", saved.RawPayloadJson); Assert.Equal("[{\"code\":\"warning\"}]", saved.ValidationErrorsJson);
        context.EmployeeImportRows.Add(Row(batch.Id, 3, "EMP-2", "not-json", null));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.InvalidTextRepresentation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportConstraints_EnforceBatchHashAndPartialExternalCodeIndex()
    {
        fixture.RequireConfigured();
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        var batch = Batch("c"); context.EmployeeImportBatches.Add(batch); await context.SaveChangesAsync();
        context.EmployeeImportBatches.Add(Batch("c"));
        var batchException = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(batchException.InnerException).SqlState);
        context.ChangeTracker.Clear();
        context.EmployeeImportRows.AddRange(Row(batch.Id, 2, null, "{}", null), Row(batch.Id, 3, null, "{}", null)); await context.SaveChangesAsync();
        context.EmployeeImportRows.AddRange(Row(batch.Id, 4, "EMP-1", "{}", null), Row(batch.Id, 5, "EMP-1", "{}", null));
        var rowException = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(rowException.InnerException).SqlState);
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportRow_ForeignKeyConstraintIsEnforced()
    {
        fixture.RequireConfigured();
        await fixture.ResetDatabaseAsync();
        await using var context = fixture.CreateDbContext();
        context.EmployeeImportRows.Add(Row(Guid.NewGuid(), 2, "EMP-1", "{}", null));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(exception.InnerException).SqlState);
    }

    private static EmployeeImportBatch Batch(string seed) => new() { Id = Guid.NewGuid(), FileName = seed + ".xlsx", FileHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(seed))).ToLowerInvariant(), DatasetSplit = EmployeeDatasetSplit.Training, ImportedAtUtc = DateTime.UtcNow, Status = EmployeeImportBatchStatus.Processing };
    private static EmployeeImportRow Row(Guid batchId, int number, string? code, string rawJson, string? errorsJson) => new() { Id = Guid.NewGuid(), ImportBatchId = batchId, SourceRowNumber = number, ExternalEmployeeCode = code, RawPayloadJson = rawJson, ValidationErrorsJson = errorsJson, ImportStatus = EmployeeImportRowStatus.Succeeded, CreatedAtUtc = DateTime.UtcNow };
}
