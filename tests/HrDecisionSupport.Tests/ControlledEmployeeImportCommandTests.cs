using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Persistence;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.EmployeeImportDryRun;

namespace HrDecisionSupport.Tests;

public sealed class ControlledEmployeeImportCommandTests
{
    [Fact]
    public async Task ExecuteAsync_MissingObservationDateRejectsBeforePreflight()
    {
        var execution = new FakeExecution(); var exit = await Command(execution).ExecuteAsync(new("sample.xlsx", null, "2", "Development"));
        Assert.Equal(1, exit); Assert.Equal(0, execution.PreflightCalls);
    }

    [Fact]
    public async Task ExecuteAsync_ProductionIsLockedBeforePreflight()
    {
        var execution = new FakeExecution(); var exit = await Command(execution).ExecuteAsync(Options(environment: "Production"));
        Assert.Equal(1, exit); Assert.Equal(0, execution.PreflightCalls);
    }

    [Theory]
    [InlineData("employee_import_runner.file_missing")]
    [InlineData("employee_import_runner.connection_missing")]
    [InlineData("employee_import_runner.database_unreachable")]
    [InlineData("employee_import_runner.pending_migrations")]
    [InlineData("employee_import_runner.pending_model_changes")]
    [InlineData("employee_import_runner.dry_run_invalid_rows")]
    [InlineData("employee_import.already_imported")]
    public async Task ExecuteAsync_PreflightRejectionDoesNotImport(string code)
    {
        var execution = new FakeExecution { Preflight = Result<ImportPreflightSummary>.Failure(code, "blocked") };
        var exit = await Command(execution).ExecuteAsync(Options());
        Assert.Equal(1, exit); Assert.Equal(1, execution.PreflightCalls); Assert.Equal(0, execution.ImportCalls);
    }

    [Fact]
    public async Task ExecuteAsync_WrongConfirmationRejectsWithoutImport()
    {
        var execution = new FakeExecution(); var confirmation = new FakeConfirmation(false); var exit = await new ControlledEmployeeImportCommand(execution, confirmation, new Output()).ExecuteAsync(Options());
        Assert.Equal(1, exit); Assert.Equal("IMPORT 2", confirmation.RequiredText); Assert.Equal(0, execution.ImportCalls);
    }

    [Fact]
    public async Task ConsoleConfirmation_RequiresTheExactPreviewRowCount()
    {
        var confirmation = new ConsoleImportConfirmation();
        Assert.True(await confirmation.ConfirmAsync("IMPORT 6800", "6800", CancellationToken.None));
        Assert.False(await confirmation.ConfirmAsync("IMPORT 6800", "6799", CancellationToken.None));
        Assert.False(await confirmation.ConfirmAsync("IMPORT 6800", "IMPORT 6800", CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_TestEnvironmentWithCorrectConfirmationImportsAndVerifiesBatch()
    {
        var execution = new FakeExecution(); var exit = await Command(execution).ExecuteAsync(Options());
        Assert.Equal(0, exit); Assert.Equal(1, execution.ImportCalls); Assert.Equal(1, execution.VerifyCalls);
    }

    [Fact]
    public async Task ExecuteAsync_FatalImportFailureReturnsNonZeroAndSkipsVerification()
    {
        var execution = new FakeExecution { Import = Result<EmployeeImportResult>.Failure("employee_import.batch_failed", "failed") };
        var exit = await Command(execution).ExecuteAsync(Options());
        Assert.Equal(1, exit); Assert.Equal(1, execution.ImportCalls); Assert.Equal(0, execution.VerifyCalls);
    }

    [Fact]
    public async Task ExecuteAsync_PreviewDoesNotWriteConnectionSecrets()
    {
        var output = new Output(); var execution = new FakeExecution(); var exit = await new ControlledEmployeeImportCommand(execution, new FakeConfirmation(true), output).ExecuteAsync(Options());
        Assert.Equal(0, exit); Assert.Contains(output.Lines, line => line.Contains("safe-host/safe-db")); Assert.DoesNotContain(output.Lines, line => line.Contains("Password", StringComparison.OrdinalIgnoreCase));
    }

    private static ControlledEmployeeImportCommand Command(FakeExecution execution) => new(execution, new FakeConfirmation(true), new Output());
    private static ControlledImportOptions Options(DateOnly? observationDate = null, string environment = "Development") => new("sample.xlsx", observationDate ?? new DateOnly(2026, 8, 6), "2", environment);

    private sealed class FakeExecution : IControlledEmployeeImportExecution
    {
        public Result<ImportPreflightSummary> Preflight { get; set; } = Result<ImportPreflightSummary>.Success(new("sample.xlsx", "hash", "Employees", 2, 0, 2, 0, 3, 2, new DateOnly(2026, 8, 6), "safe-host", "safe-db", TimeSpan.FromMilliseconds(1)));
        public Result<EmployeeImportResult> Import { get; set; } = Result<EmployeeImportResult>.Success(new(Guid.NewGuid(), false, "sample.xlsx", EmployeeDatasetSplit.Training, 2, 0, 2, 0, 2, 0, 0, 1, 0, 0, 0, 0, 0, 0, 2, 2, [new(2, "E1", EmployeeImportRowStatus.SucceededWithWarnings, Guid.NewGuid(), []), new(3, "E2", EmployeeImportRowStatus.SucceededWithWarnings, Guid.NewGuid(), [])]));
        public int PreflightCalls { get; private set; } public int ImportCalls { get; private set; } public int VerifyCalls { get; private set; }
        public Task<Result<ImportPreflightSummary>> PreflightAsync(ControlledImportOptions options, CancellationToken cancellationToken) { PreflightCalls++; return Task.FromResult(Preflight); }
        public Task<Result<EmployeeImportResult>> ImportAsync(ControlledImportOptions options, CancellationToken cancellationToken) { ImportCalls++; return Task.FromResult(Import); }
        public Task<ImportVerificationSummary> VerifyAsync(EmployeeImportResult result, TimeSpan elapsed, CancellationToken cancellationToken) { VerifyCalls++; return Task.FromResult(new ImportVerificationSummary(result.ImportBatchId!.Value, "Completed", 2, 0, 2, 0, 2, 0, 2, 0, 2, 1, 0, 0, 0, 0, 0, 0, 0, 2, 2, elapsed)); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class FakeConfirmation(bool approved) : IImportConfirmation { public string? RequiredText { get; private set; } public Task<bool> ConfirmAsync(string requiredText, string? suppliedRowCount, CancellationToken cancellationToken) { RequiredText = requiredText; return Task.FromResult(approved); } }
    private sealed class Output : IImportOutput { public List<string> Lines { get; } = []; public void WriteLine(string message) => Lines.Add(message); public void WriteError(string message) => Lines.Add(message); }
}
