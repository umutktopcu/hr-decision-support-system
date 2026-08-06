using System.Text;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Persistence;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.EmployeeImports;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

public sealed class EmployeeImportCancellationTests
{
    [Fact]
    public async Task ImportAsync_WhenCancelledBeforeBatchCreation_RethrowsAndCreatesNoBatch()
    {
        await using var db = TestDatabase.CreateContext(); using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(db, [Source(2, "E1")]).ImportAsync(Request("before"), cancellation.Token));
        Assert.Empty(db.EmployeeImportBatches);
    }

    [Fact]
    public async Task ImportAsync_WhenCancelledAfterCommittedRow_FinalizesBatchAsFailedAndRethrows()
    {
        await using var db = TestDatabase.CreateContext(); using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service(db, [Source(2, "E1"), Source(3, "E2")], orchestrationHook: new CancelBeforeRowHook(3, cancellation)).ImportAsync(Request("after-first"), cancellation.Token));
        var batch = await db.EmployeeImportBatches.SingleAsync(); Assert.Equal(EmployeeImportBatchStatus.Failed, batch.Status); Assert.Equal(1, batch.TotalRowCount); Assert.Equal(1, batch.SuccessfulRowCount); Assert.Equal(0, batch.FailedRowCount); Assert.Single(await db.Employees.ToListAsync());
    }

    [Fact]
    public async Task ImportAsync_WhenCatalogOrchestrationFails_FinalizesBatchAsFailedWithStableError()
    {
        await using var db = TestDatabase.CreateContext(); var result = await Service(db, [Source(2, "E1")], orchestrationHook: new ThrowAfterCatalogHook()).ImportAsync(Request("fatal"));
        Assert.True(result.IsFailure); Assert.Equal("employee_import.batch_failed", result.Error!.Code); var batch = await db.EmployeeImportBatches.SingleAsync(); Assert.Equal(EmployeeImportBatchStatus.Failed, batch.Status); Assert.Equal(0, batch.TotalRowCount); Assert.Empty(db.EmployeeImportRows);
    }

    [Fact]
    public async Task ImportAsync_WhenRowFailsAndLaterRowSucceeds_CompletesWithErrors()
    {
        await using var db = TestDatabase.CreateContext(); var result = await Service(db, [Source(2, "E1"), Source(3, "E2")], persistenceHook: new ThrowForRowHook(2)).ImportAsync(Request("row-failure"));
        Assert.True(result.IsSuccess); Assert.Equal(EmployeeImportBatchStatus.CompletedWithErrors, (await db.EmployeeImportBatches.SingleAsync()).Status); Assert.Equal(1, result.Value.FailedRows); Assert.Equal(1, result.Value.SucceededRows);
    }

    [Fact]
    public async Task ImportAsync_WhenAllRowsSucceed_CompletesBatch()
    {
        await using var db = TestDatabase.CreateContext(); var result = await Service(db, [Source(2, "E1")]).ImportAsync(Request("success"));
        Assert.True(result.IsSuccess); Assert.Equal(EmployeeImportBatchStatus.Completed, (await db.EmployeeImportBatches.SingleAsync()).Status);
    }

    private static EmployeeImportService Service(HrDecisionSupportDbContext db, IReadOnlyList<EmployeeImportSourceRow> rows, IEmployeeImportRowPersistenceHook? persistenceHook = null, IEmployeeImportOrchestrationHook? orchestrationHook = null)
    {
        var reader = new Reader(rows); var normalizer = new EmployeeImportRowNormalizer(); var validator = new EmployeeImportRowValidator(); return new(db, reader, normalizer, validator, new EmployeeImportDryRunService(reader, normalizer, validator), new EmployeeImportTransactionRunner(db), TimeProvider.System, persistenceHook, orchestrationHook);
    }

    private static EmployeeImportRequest Request(string name) => new(new MemoryStream(Encoding.UTF8.GetBytes(name)), name + ".xlsx", EmployeeDatasetSplit.Training, new DateOnly(2025, 1, 1), "v1", "l1", false);
    private static EmployeeImportSourceRow Source(int rowNumber, string employeeCode) => new(rowNumber, employeeCode, "Backend", 5, 4, null, "C#", null, "Project", null, null, null, null, null, null, new DateOnly(2020, 1, 1), null, 1, null, null, null, null, 0, 0, 1, new Dictionary<string, string?>(), []);
    private sealed class Reader(IReadOnlyList<EmployeeImportSourceRow> rows) : IEmployeeSpreadsheetReader { public Task<Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(Stream content, CancellationToken cancellationToken = default) { cancellationToken.ThrowIfCancellationRequested(); return Task.FromResult(Result<EmployeeImportSpreadsheetReadResult>.Success(new("Employees", [], rows, []))); } }
    private sealed class CancelBeforeRowHook(int sourceRowNumber, CancellationTokenSource cancellation) : IEmployeeImportOrchestrationHook { public void AfterCatalogPrepared() { } public void BeforeRow(EmployeeImportNormalizedRow row) { if (row.SourceRowNumber == sourceRowNumber) cancellation.Cancel(); } }
    private sealed class ThrowAfterCatalogHook : IEmployeeImportOrchestrationHook { public void AfterCatalogPrepared() => throw new InvalidOperationException(); public void BeforeRow(EmployeeImportNormalizedRow row) { } }
    private sealed class ThrowForRowHook(int sourceRowNumber) : IEmployeeImportRowPersistenceHook { public void AfterPersonAndEmployeePrepared(EmployeeImportNormalizedRow row) { if (row.SourceRowNumber == sourceRowNumber) throw new InvalidOperationException(); } }
}
