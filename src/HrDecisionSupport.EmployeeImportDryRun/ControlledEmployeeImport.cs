using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Persistence;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.EmployeeImports;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace HrDecisionSupport.EmployeeImportDryRun;

public sealed record ControlledImportOptions(string? FilePath, DateOnly? ObservationDate, string? ConfirmRowCount, string EnvironmentName);
public sealed record ImportPreflightSummary(string FileName, string FileHash, string WorksheetName, int TotalRows, int ValidRows, int WarningRows, int InvalidRows, int WarningCount, int UnknownCompetencyCount, DateOnly ObservationDate, string DatabaseHost, string DatabaseName, TimeSpan DryRunElapsed);
public sealed record ImportVerificationSummary(Guid BatchId, string BatchStatus, int TotalRows, int SucceededRows, int SucceededWithWarningsRows, int FailedRows, int EmployeesCreated, int EmployeesUpdated, int PersonsCreated, int PersonsReused, int FeatureSnapshots, int CompetencyLinks, int SectorExperiences, int EducationRecords, int CertificateLinks, int LanguageLinks, int WorkModeLinks, int PreviousPositionEvidence, int AssignmentsInEmployeeScope, int DiagnosticCount, int UnknownCompetencyDiagnosticCount, TimeSpan Elapsed);

public interface IControlledEmployeeImportExecution : IAsyncDisposable
{
    Task<Result<ImportPreflightSummary>> PreflightAsync(ControlledImportOptions options, CancellationToken cancellationToken);
    Task<Result<EmployeeImportResult>> ImportAsync(ControlledImportOptions options, CancellationToken cancellationToken);
    Task<ImportVerificationSummary> VerifyAsync(EmployeeImportResult result, TimeSpan elapsed, CancellationToken cancellationToken);
}

public interface IImportConfirmation { Task<bool> ConfirmAsync(string requiredText, string? suppliedRowCount, CancellationToken cancellationToken); }
public interface IImportOutput { void WriteLine(string message); void WriteError(string message); }

public sealed class ControlledEmployeeImportCommand(IControlledEmployeeImportExecution execution, IImportConfirmation confirmation, IImportOutput output)
{
    public async Task<int> ExecuteAsync(ControlledImportOptions options, CancellationToken cancellationToken = default)
    {
        if (options.ObservationDate is null) return Fail("employee_import_runner.observation_date_required", "--observation-date is required.");
        if (string.Equals(options.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase)) return Fail("employee_import_runner.production_locked", "Production import is locked in this runner.");
        if (!IsTestOrDevelopment(options.EnvironmentName)) return Fail("employee_import_runner.environment_not_allowed", "Target environment must be explicitly Development, Test, or Integration.");

        var preflight = await execution.PreflightAsync(options, cancellationToken);
        if (preflight.IsFailure) return Fail(preflight.Error!.Code, preflight.Error.Message);
        var summary = preflight.Value;
        output.WriteLine($"Import preview: file={summary.FileName}; worksheet={summary.WorksheetName}; rows={summary.TotalRows}; valid={summary.ValidRows}; warnings={summary.WarningRows}; invalid={summary.InvalidRows}; warning-count={summary.WarningCount}; unknown-competency={summary.UnknownCompetencyCount}; observation-date={summary.ObservationDate:yyyy-MM-dd}; database={summary.DatabaseHost}/{summary.DatabaseName}; hash={summary.FileHash}; dry-run-ms={summary.DryRunElapsed.TotalMilliseconds:F0}.");
        var required = $"IMPORT {summary.TotalRows}";
        if (!await confirmation.ConfirmAsync(required, options.ConfirmRowCount, cancellationToken)) return Fail("employee_import_runner.confirmation_rejected", $"Confirmation is required. Type {required} to continue.");

        var timer = Stopwatch.StartNew();
        var imported = await execution.ImportAsync(options, cancellationToken); timer.Stop();
        if (imported.IsFailure) return Fail(imported.Error!.Code, imported.Error.Message);
        var verification = await execution.VerifyAsync(imported.Value, timer.Elapsed, cancellationToken);
        output.WriteLine($"Import verification: batch={verification.BatchId}; status={verification.BatchStatus}; rows={verification.TotalRows}; succeeded={verification.SucceededRows}; warnings={verification.SucceededWithWarningsRows}; failed={verification.FailedRows}; employees-created={verification.EmployeesCreated}; employees-updated={verification.EmployeesUpdated}; persons-created={verification.PersonsCreated}; persons-reused={verification.PersonsReused}; snapshots={verification.FeatureSnapshots}; assignments-in-scope={verification.AssignmentsInEmployeeScope}; previous-positions={verification.PreviousPositionEvidence}; competency-links={verification.CompetencyLinks}; sectors={verification.SectorExperiences}; education={verification.EducationRecords}; certificates={verification.CertificateLinks}; languages={verification.LanguageLinks}; work-modes={verification.WorkModeLinks}; diagnostics={verification.DiagnosticCount}; unknown-competency={verification.UnknownCompetencyDiagnosticCount}; elapsed-ms={verification.Elapsed.TotalMilliseconds:F0}.");
        return 0;
    }

    private int Fail(string code, string message) { output.WriteError($"{code}: {message}"); return 1; }
    private static bool IsTestOrDevelopment(string environment) => string.Equals(environment, "Development", StringComparison.OrdinalIgnoreCase) || string.Equals(environment, "Test", StringComparison.OrdinalIgnoreCase) || string.Equals(environment, "Integration", StringComparison.OrdinalIgnoreCase);
}

public sealed class ConsoleImportConfirmation : IImportConfirmation
{
    public Task<bool> ConfirmAsync(string requiredText, string? suppliedRowCount, CancellationToken cancellationToken)
    {
        var separator = requiredText.IndexOf(' '); var expectedCount = separator >= 0 ? requiredText[(separator + 1)..] : requiredText;
        if (!string.IsNullOrWhiteSpace(suppliedRowCount)) return Task.FromResult(string.Equals(suppliedRowCount, expectedCount, StringComparison.Ordinal));
        if (Console.IsInputRedirected) return Task.FromResult(false);
        Console.Write($"Type {requiredText} to continue: ");
        return Task.FromResult(string.Equals(Console.ReadLine(), requiredText, StringComparison.Ordinal));
    }
}
public sealed class ConsoleImportOutput : IImportOutput { public void WriteLine(string message) => Console.WriteLine(message); public void WriteError(string message) => Console.Error.WriteLine(message); }

public sealed class PostgreSqlEmployeeImportExecution(string? connectionString) : IControlledEmployeeImportExecution
{
    private HrDecisionSupportDbContext? _context;
    private EmployeeImportService? _service;
    private string? _fileHash;
    private HashSet<string> _existingPersonCodes = new(StringComparer.Ordinal);
    public async Task<Result<ImportPreflightSummary>> PreflightAsync(ControlledImportOptions options, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(options.FilePath) || !File.Exists(options.FilePath)) return Result<ImportPreflightSummary>.Failure("employee_import_runner.file_missing", "HRDS_EMPLOYEE_IMPORT_FILE is missing or does not exist.");
        if (options.ObservationDate is null) return Result<ImportPreflightSummary>.Failure("employee_import_runner.observation_date_required", "Observation date is required.");
        if (string.IsNullOrWhiteSpace(connectionString)) return Result<ImportPreflightSummary>.Failure("employee_import_runner.connection_missing", "A PostgreSQL connection string is required.");
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString);
            _context = new(new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
                .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector())
                .Options);
            if (!await _context.Database.CanConnectAsync(cancellationToken)) return Result<ImportPreflightSummary>.Failure("employee_import_runner.database_unreachable", "The target database is not reachable.");
            if ((await _context.Database.GetPendingMigrationsAsync(cancellationToken)).Any()) return Result<ImportPreflightSummary>.Failure("employee_import_runner.pending_migrations", "All migrations must be applied before import.");
            if (_context.Database.HasPendingModelChanges()) return Result<ImportPreflightSummary>.Failure("employee_import_runner.pending_model_changes", "Pending EF model changes must be resolved before import.");
            _fileHash = await HashAsync(options.FilePath, cancellationToken);
            if (await _context.EmployeeImportBatches.AnyAsync(batch => batch.FileHash == _fileHash, cancellationToken)) return Result<ImportPreflightSummary>.Failure("employee_import.already_imported", "A batch with this file hash already exists.");
            var reader = new ClosedXmlEmployeeSpreadsheetReader(); var normalizer = new EmployeeImportRowNormalizer(); var validator = new EmployeeImportRowValidator(); var dryRun = new EmployeeImportDryRunService(reader, normalizer, validator);
            var timer = Stopwatch.StartNew(); await using var stream = File.OpenRead(options.FilePath); var result = await dryRun.DryRunAsync(stream, new(EmployeeDatasetSplit.Training, options.ObservationDate), cancellationToken); timer.Stop();
            if (result.IsFailure) return Result<ImportPreflightSummary>.Failure(result.Error!);
            if (result.Value.InvalidRows != 0) return Result<ImportPreflightSummary>.Failure("employee_import_runner.dry_run_invalid_rows", "Dry-run produced invalid rows; import is blocked.");
            var employeeCodes = result.Value.Rows.Select(row => row.EmployeeCode).Where(code => !string.IsNullOrWhiteSpace(code)).Cast<string>().Distinct(StringComparer.Ordinal).ToArray();
            _existingPersonCodes = (await _context.People.Where(person => employeeCodes.Contains(person.AnonymousCode)).Select(person => person.AnonymousCode).ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
            _service = new EmployeeImportService(_context, reader, normalizer, validator, dryRun, new EmployeeImportTransactionRunner(_context), TimeProvider.System);
            var diagnostics = result.Value.Rows.SelectMany(row => row.Diagnostics).ToArray();
            return Result<ImportPreflightSummary>.Success(new(Path.GetFileName(options.FilePath), _fileHash, result.Value.WorksheetName, result.Value.TotalRows, result.Value.ValidRows, result.Value.ValidWithWarningsRows, result.Value.InvalidRows, result.Value.WarningCount, diagnostics.Count(diagnostic => diagnostic.Code == "unknown_competency"), options.ObservationDate.Value, builder.Host ?? string.Empty, builder.Database ?? string.Empty, timer.Elapsed));
        }
        catch (OperationCanceledException) { throw; }
        catch { return Result<ImportPreflightSummary>.Failure("employee_import_runner.preflight_failed", "Import preflight could not be completed."); }
    }

    public async Task<Result<EmployeeImportResult>> ImportAsync(ControlledImportOptions options, CancellationToken cancellationToken)
    {
        if (_service is null || _fileHash is null || options.FilePath is null || options.ObservationDate is null) return Result<EmployeeImportResult>.Failure("employee_import_runner.preflight_required", "Successful preflight is required before import.");
        await using var stream = File.OpenRead(options.FilePath);
        return await _service.ImportAsync(new(stream, Path.GetFileName(options.FilePath), EmployeeDatasetSplit.Training, options.ObservationDate, "v1", "l1", false), cancellationToken);
    }

    public async Task<ImportVerificationSummary> VerifyAsync(EmployeeImportResult result, TimeSpan elapsed, CancellationToken cancellationToken)
    {
        var context = _context ?? throw new InvalidOperationException("Preflight is required."); var batchId = result.ImportBatchId ?? throw new InvalidOperationException("Import did not produce a batch.");
        var batch = await context.EmployeeImportBatches.SingleAsync(value => value.Id == batchId, cancellationToken);
        var rows = await context.EmployeeImportRows.Where(row => row.ImportBatchId == batchId).ToListAsync(cancellationToken);
        var employeeIds = rows.Where(row => row.EmployeeId.HasValue).Select(row => row.EmployeeId!.Value).Distinct().ToArray();
        var diagnosticDocuments = rows.Where(row => !string.IsNullOrWhiteSpace(row.ValidationErrorsJson)).Select(row => row.ValidationErrorsJson!).ToArray();
        var diagnostics = diagnosticDocuments.Sum(json => CountDiagnostics(json)); var unknown = diagnosticDocuments.Sum(json => CountDiagnostics(json, "unknown_competency"));
        var importedCodes = result.Rows.Select(row => row.EmployeeCode).Where(code => !string.IsNullOrWhiteSpace(code)).Cast<string>().Distinct(StringComparer.Ordinal).ToArray();
        return new(batchId, batch.Status.ToString(), batch.TotalRowCount, rows.Count(row => row.ImportStatus == EmployeeImportRowStatus.Succeeded), rows.Count(row => row.ImportStatus == EmployeeImportRowStatus.SucceededWithWarnings), rows.Count(row => row.ImportStatus == EmployeeImportRowStatus.Failed), result.NewEmployees, result.UpdatedEmployees, importedCodes.Count(code => !_existingPersonCodes.Contains(code)), importedCodes.Count(code => _existingPersonCodes.Contains(code)), result.FeatureSnapshotsCreated, result.CompetencyLinksCreated, result.SectorLinksCreated, result.EducationRecordsCreated, result.CertificateLinksCreated, result.LanguageLinksCreated, result.WorkModeLinksCreated, await context.PersonPriorPositionEvidences.CountAsync(evidence => rows.Select(row => row.Id).Contains(evidence.ImportRowId), cancellationToken), await context.EmployeeAssignments.CountAsync(assignment => employeeIds.Contains(assignment.EmployeeId), cancellationToken), diagnostics, unknown, elapsed);
    }

    public ValueTask DisposeAsync() => _context is null ? ValueTask.CompletedTask : _context.DisposeAsync();
    private static async Task<string> HashAsync(string path, CancellationToken cancellationToken) { await using var stream = File.OpenRead(path); return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant(); }
    private static int CountDiagnostics(string json, string? code = null) { using var document = JsonDocument.Parse(json); return document.RootElement.EnumerateArray().Count(item => code is null || (item.TryGetProperty("code", out var value) && value.GetString() == code)); }
}
