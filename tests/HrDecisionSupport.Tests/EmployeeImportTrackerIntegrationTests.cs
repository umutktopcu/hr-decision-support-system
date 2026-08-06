using System.Text;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Persistence;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.EmployeeImports;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace HrDecisionSupport.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class EmployeeImportTrackerIntegrationTests(PostgreSqlIntegrationTestFixture fixture, ITestOutputHelper output)
{
    [Fact]
    public async Task ImportAsync_OneHundredAndOneRows_DetachesCommittedRowGraphsInMemory()
    {
        await using var db = TestDatabase.CreateContext(); var observer = new TrackerObserver();
        var rows = Enumerable.Range(1, 101).Select(i => Source(i + 1, "MEM" + i.ToString("000"))).ToArray(); var result = await Service(db, rows, observer).ImportAsync(Request("tracker-inmemory"));
        output.WriteLine(observer.Describe());
        Assert.True(result.IsSuccess); Assert.Equal(101, result.Value.SucceededRows); Assert.Equal(101, await db.Employees.CountAsync()); var cleanupBefore = Assert.Single(observer.CleanupBefore); var cleanupAfter = Assert.Single(observer.CleanupAfter); Assert.True(cleanupAfter < cleanupBefore / 2);
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_ThreeHundredRows_CleansRowGraphsAtSafeIntervals()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync(); await using var db = fixture.CreateDbContext(); var observer = new TrackerObserver();
        var rows = Enumerable.Range(1, 300).Select(i => Source(i + 1, "TRACK" + i.ToString("000"))).ToArray();
        var result = await Service(db, rows, observer).ImportAsync(Request("tracker-three-hundred"));
        output.WriteLine(observer.Describe());
        Assert.True(result.IsSuccess); Assert.Equal(300, result.Value.SucceededRows); Assert.Equal(300, await db.Employees.CountAsync()); Assert.Equal(EmployeeImportBatchStatus.Completed, (await db.EmployeeImportBatches.SingleAsync()).Status); Assert.Equal(1, await db.Competencies.CountAsync()); Assert.Equal(300, await db.PersonCompetencies.CountAsync()); Assert.Equal(1, await db.Sectors.CountAsync()); Assert.Equal(300, await db.PersonSectorExperiences.CountAsync());
        Assert.True(observer.CleanupBefore.Count >= 2); Assert.Equal(observer.CleanupBefore.Count, observer.CleanupAfter.Count); Assert.All(observer.CleanupBefore.Zip(observer.CleanupAfter), pair => Assert.True(pair.Second < pair.First / 2, $"before={pair.First}, after={pair.Second}")); Assert.True(observer.MaximumTrackedEntries < 3_500, $"max tracked entries: {observer.MaximumTrackedEntries}");
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_FailedHundredthRow_CleansAfterFailureAndContinues()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync(); await using var db = fixture.CreateDbContext(); var observer = new TrackerObserver();
        var rows = Enumerable.Range(1, 101).Select(i => Source(i + 1, "ROLL" + i.ToString("000"))).ToArray();
        var result = await Service(db, rows, observer, new ThrowOnRowHook(101)).ImportAsync(Request("tracker-rollback-boundary"));
        Assert.True(result.IsSuccess); Assert.Equal(100, result.Value.SucceededRows); Assert.Equal(1, result.Value.FailedRows); Assert.Equal(EmployeeImportBatchStatus.CompletedWithErrors, (await db.EmployeeImportBatches.SingleAsync()).Status); Assert.Equal(100, await db.Employees.CountAsync()); var cleanupBefore = Assert.Single(observer.CleanupBefore); var cleanupAfter = Assert.Single(observer.CleanupAfter); Assert.True(cleanupAfter < cleanupBefore / 2);
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_CancellationAfterCleanup_RollsBackCurrentRowAndFinalizesBatch()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync(); await using var db = fixture.CreateDbContext(); var observer = new TrackerObserver(); using var cancellation = new CancellationTokenSource();
        var rows = Enumerable.Range(1, 101).Select(i => Source(i + 1, "CANCEL" + i.ToString("000"))).ToArray();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(db, rows, observer, new CancelOnRowHook(102, cancellation)).ImportAsync(Request("tracker-cancel-boundary"), cancellation.Token));
        await using var verification = fixture.CreateDbContext(); var batch = await verification.EmployeeImportBatches.SingleAsync(); Assert.Equal(EmployeeImportBatchStatus.Failed, batch.Status); Assert.Equal(100, await verification.Employees.CountAsync()); var cleanupBefore = Assert.Single(observer.CleanupBefore); var cleanupAfter = Assert.Single(observer.CleanupAfter); Assert.True(cleanupAfter < cleanupBefore / 2);
    }

    private static EmployeeImportService Service(HrDecisionSupportDbContext db, IReadOnlyList<EmployeeImportSourceRow> rows, IEmployeeImportOrchestrationHook observer, IEmployeeImportRowPersistenceHook? persistenceHook = null)
    {
        var reader = new Reader(rows); var normalizer = new EmployeeImportRowNormalizer(); var validator = new EmployeeImportRowValidator();
        return new(db, reader, normalizer, validator, new EmployeeImportDryRunService(reader, normalizer, validator), new EmployeeImportTransactionRunner(db), TimeProvider.System, persistenceHook, observer);
    }
    private static EmployeeImportRequest Request(string name) => new(new MemoryStream(Encoding.UTF8.GetBytes(name)), name + ".xlsx", EmployeeDatasetSplit.Training, new DateOnly(2025, 1, 1), "v1", "l1", false);
    private static EmployeeImportSourceRow Source(int rowNumber, string employeeCode) => new(rowNumber, employeeCode, "Backend", 5, 4, "Developer", "C#", null, "Project", "ERP (12 ay)", "Lisans", "Computer Science", "AWS", "English C1", "Ofis", new DateOnly(2020, 1, 1), null, 5, 12, 2, 6, 4, 1, .2m, 1, new Dictionary<string, string?> { ["EmployeeCode"] = employeeCode }, []);
    private sealed class Reader(IReadOnlyList<EmployeeImportSourceRow> rows) : IEmployeeSpreadsheetReader { public Task<Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(Stream content, CancellationToken cancellationToken = default) => Task.FromResult(Result<EmployeeImportSpreadsheetReadResult>.Success(new("Employees", [], rows, []))); }
    private sealed class TrackerObserver : IEmployeeImportOrchestrationHook
    {
        public List<int> CleanupBefore { get; } = []; public List<int> CleanupAfter { get; } = []; public int MaximumTrackedEntries { get; private set; }
        public void AfterCatalogPrepared() { } public void BeforeRow(EmployeeImportNormalizedRow row) { }
        public void AfterRowPersistence(int trackedEntryCount) => MaximumTrackedEntries = Math.Max(MaximumTrackedEntries, trackedEntryCount);
        public void BeforeTrackerCleanup(int trackedEntryCount) => CleanupBefore.Add(trackedEntryCount);
        public void AfterTrackerCleanup(int trackedEntryCount) => CleanupAfter.Add(trackedEntryCount);
        public string Describe() => $"max={MaximumTrackedEntries}; cleanup before=[{string.Join(',', CleanupBefore)}]; after=[{string.Join(',', CleanupAfter)}]";
    }
    private sealed class ThrowOnRowHook(int sourceRowNumber) : IEmployeeImportRowPersistenceHook { public void AfterPersonAndEmployeePrepared(EmployeeImportNormalizedRow row) { if (row.SourceRowNumber == sourceRowNumber) throw new InvalidOperationException(); } }
    private sealed class CancelOnRowHook(int sourceRowNumber, CancellationTokenSource cancellation) : IEmployeeImportRowPersistenceHook { public void AfterPersonAndEmployeePrepared(EmployeeImportNormalizedRow row) { if (row.SourceRowNumber == sourceRowNumber) cancellation.Cancel(); } }
}
