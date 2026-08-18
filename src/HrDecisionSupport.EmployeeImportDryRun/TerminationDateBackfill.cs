using System.Globalization;
using System.Text.Json;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace HrDecisionSupport.EmployeeImportDryRun;

public sealed record TerminationDateBackfillOptions(Guid? BatchId, string? ConfirmCandidateCount, string EnvironmentName);
public enum TerminationDateBackfillCategory { SourceNullDbNull, SourceMatchesDb, SourceFilledDbNull, SourceDiffersDb, SourceUnparseable, EmployeeUnmatched, SourceNullDbNonNull }
public sealed record TerminationDateBackfillRow(Guid ImportRowId, int SourceRowNumber, Guid? EmployeeId, DateOnly? HireDate, DateOnly? PersistedTerminationDate, string? SourceTerminationRaw);
public sealed record TerminationDateBackfillCandidate(Guid ImportRowId, Guid EmployeeId, DateOnly SourceTerminationDate);
public sealed record TerminationDateBackfillPreview(Guid BatchId, string BatchStatus, int TotalImportRows, IReadOnlyDictionary<TerminationDateBackfillCategory, int> Categories, int CandidateCount, int CandidateDistinctEmployees, int ManualReviewCount, string DatabaseHost, string DatabaseName, IReadOnlyList<TerminationDateBackfillCandidate> Candidates)
{
    public int Count(TerminationDateBackfillCategory category) => Categories.GetValueOrDefault(category);
}
public sealed record TerminationDateBackfillResult(int UpdatedCount, int SkippedCount, int FailedCount, DateTime StartedAtUtc, DateTime CompletedAtUtc);
public interface ITerminationDateBackfillExecution : IAsyncDisposable
{
    Task<Result<TerminationDateBackfillPreview>> PreviewAsync(TerminationDateBackfillOptions options, CancellationToken cancellationToken);
    Task<Result<TerminationDateBackfillResult>> BackfillAsync(TerminationDateBackfillOptions options, int expectedCandidateCount, CancellationToken cancellationToken);
}

public sealed class TerminationDateBackfillPlanner
{
    public TerminationDateBackfillPreview Plan(Guid batchId, string batchStatus, IEnumerable<TerminationDateBackfillRow> rows, string host, string database)
    {
        var classified = rows.Select(row => new { Row = row, SourceDate = Parse(row.SourceTerminationRaw) }).ToArray();
        var conflictingEmployees = classified.Where(x => x.Row.EmployeeId.HasValue && x.SourceDate.HasValue).GroupBy(x => x.Row.EmployeeId!.Value).Where(group => group.Select(x => x.SourceDate!.Value).Distinct().Count() > 1).Select(group => group.Key).ToHashSet();
        var categories = Enum.GetValues<TerminationDateBackfillCategory>().ToDictionary(category => category, _ => 0);
        var candidates = new List<TerminationDateBackfillCandidate>(); var manual = 0;
        foreach (var item in classified)
        {
            var row = item.Row; var source = item.SourceDate;
            var category = row.EmployeeId is null || row.HireDate is null ? TerminationDateBackfillCategory.EmployeeUnmatched
                : !string.IsNullOrWhiteSpace(row.SourceTerminationRaw) && source is null ? TerminationDateBackfillCategory.SourceUnparseable
                : source is null && row.PersistedTerminationDate is null ? TerminationDateBackfillCategory.SourceNullDbNull
                : source is null ? TerminationDateBackfillCategory.SourceNullDbNonNull
                : row.PersistedTerminationDate is null ? TerminationDateBackfillCategory.SourceFilledDbNull
                : source == row.PersistedTerminationDate ? TerminationDateBackfillCategory.SourceMatchesDb
                : TerminationDateBackfillCategory.SourceDiffersDb;
            categories[category]++;
            if (category == TerminationDateBackfillCategory.SourceFilledDbNull && source!.Value >= row.HireDate && !conflictingEmployees.Contains(row.EmployeeId!.Value)) candidates.Add(new(row.ImportRowId, row.EmployeeId.Value, source.Value));
            else if (category is TerminationDateBackfillCategory.SourceFilledDbNull or TerminationDateBackfillCategory.SourceDiffersDb or TerminationDateBackfillCategory.SourceUnparseable or TerminationDateBackfillCategory.EmployeeUnmatched) manual++;
        }
        return new(batchId, batchStatus, classified.Length, categories, candidates.Count, candidates.Select(x => x.EmployeeId).Distinct().Count(), manual, host, database, candidates);
    }

    public static DateOnly? Parse(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var text = raw.Trim(); var formats = new[] { "dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd" };
        if (DateOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var exact)) return exact;
        if (DateOnly.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var invariant)) return invariant;
        return DateOnly.TryParse(text, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out var turkish) ? turkish : null;
    }
}

public sealed class TerminationDateBackfillCommand(ITerminationDateBackfillExecution execution, IImportConfirmation confirmation, IImportOutput output)
{
    public async Task<int> ExecuteAsync(TerminationDateBackfillOptions options, CancellationToken cancellationToken = default)
    {
        if (options.BatchId is null) return Fail("termination_date_backfill.batch_id_required", "--batch-id is required.");
        if (string.Equals(options.EnvironmentName, "Production", StringComparison.OrdinalIgnoreCase)) return Fail("termination_date_backfill.production_locked", "Production backfill is locked.");
        if (!Allowed(options.EnvironmentName)) return Fail("termination_date_backfill.environment_not_allowed", "Target environment must be Development, Test, or Integration.");
        var preview = await execution.PreviewAsync(options, cancellationToken);
        if (preview.IsFailure) return Fail(preview.Error!.Code, preview.Error.Message);
        var value = preview.Value;
        output.WriteLine($"Termination-date backfill preview: batch={value.BatchId}; status={value.BatchStatus}; total-import-rows={value.TotalImportRows}; candidate-rows={value.CandidateCount}; candidate-employees={value.CandidateDistinctEmployees}; source-null={value.Count(TerminationDateBackfillCategory.SourceNullDbNull)}; already-matching={value.Count(TerminationDateBackfillCategory.SourceMatchesDb)}; different-date={value.Count(TerminationDateBackfillCategory.SourceDiffersDb)}; parse-error={value.Count(TerminationDateBackfillCategory.SourceUnparseable)}; unmatched={value.Count(TerminationDateBackfillCategory.EmployeeUnmatched)}; database={value.DatabaseHost}/{value.DatabaseName}; mode=backfill; expected-updates={value.CandidateCount}.");
        var required = $"BACKFILL {value.CandidateCount}";
        if (!await confirmation.ConfirmAsync(required, options.ConfirmCandidateCount, cancellationToken)) return Fail("termination_date_backfill.confirmation_rejected", $"Confirmation is required. Type {required} to continue.");
        var result = await execution.BackfillAsync(options, value.CandidateCount, cancellationToken);
        if (result.IsFailure) return Fail(result.Error!.Code, result.Error.Message);
        var post = await execution.PreviewAsync(options, cancellationToken);
        if (post.IsFailure) return Fail(post.Error!.Code, post.Error.Message);
        var decision = post.Value.CandidateCount == 0 && post.Value.Count(TerminationDateBackfillCategory.SourceDiffersDb) == 0 && post.Value.Count(TerminationDateBackfillCategory.SourceUnparseable) == 0 && post.Value.Count(TerminationDateBackfillCategory.EmployeeUnmatched) == 0 ? "NO_REMEDIATION_REQUIRED" : "MANUAL_REVIEW_REQUIRED";
        await TerminationDateBackfillReportWriter.WriteAsync(options.BatchId.Value, options.EnvironmentName, value.CandidateCount, result.Value, post.Value.CandidateCount, decision, cancellationToken);
        output.WriteLine($"Termination-date backfill completed: updated={result.Value.UpdatedCount}; skipped={result.Value.SkippedCount}; failed={result.Value.FailedCount}; second-run-candidates={post.Value.CandidateCount}; post-audit={decision}.");
        return 0;
    }
    private int Fail(string code, string message) { output.WriteError($"{code}: {message}"); return 1; }
    private static bool Allowed(string value) => value.Equals("Development", StringComparison.OrdinalIgnoreCase) || value.Equals("Test", StringComparison.OrdinalIgnoreCase) || value.Equals("Integration", StringComparison.OrdinalIgnoreCase);
}

public sealed class PostgreSqlTerminationDateBackfillExecution(string? connectionString) : ITerminationDateBackfillExecution
{
    private readonly TerminationDateBackfillPlanner _planner = new();
    public async Task<Result<TerminationDateBackfillPreview>> PreviewAsync(TerminationDateBackfillOptions options, CancellationToken cancellationToken)
    {
        if (options.BatchId is null) return Result<TerminationDateBackfillPreview>.Failure("termination_date_backfill.batch_id_required", "Batch id is required.");
        if (string.IsNullOrWhiteSpace(connectionString)) return Result<TerminationDateBackfillPreview>.Failure("termination_date_backfill.connection_missing", "A PostgreSQL connection string is required.");
        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connectionString); await using var context = CreateContext();
            var batch = await context.EmployeeImportBatches.SingleOrDefaultAsync(x => x.Id == options.BatchId, cancellationToken);
            if (batch is null) return Result<TerminationDateBackfillPreview>.Failure("termination_date_backfill.batch_not_found", "The requested import batch does not exist.");
            var rows = await LoadRowsAsync(context, options.BatchId.Value, cancellationToken);
            return Result<TerminationDateBackfillPreview>.Success(_planner.Plan(batch.Id, batch.Status.ToString(), rows, builder.Host ?? string.Empty, builder.Database ?? string.Empty));
        }
        catch (OperationCanceledException) { throw; }
        catch { return Result<TerminationDateBackfillPreview>.Failure("termination_date_backfill.preview_failed", "Termination-date preview could not be completed."); }
    }
    public async Task<Result<TerminationDateBackfillResult>> BackfillAsync(TerminationDateBackfillOptions options, int expectedCandidateCount, CancellationToken cancellationToken)
    {
        if (options.BatchId is null || string.IsNullOrWhiteSpace(connectionString)) return Result<TerminationDateBackfillResult>.Failure("termination_date_backfill.preflight_required", "A batch id and connection are required.");
        var started = DateTime.UtcNow;
        await using var context = CreateContext(); await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var batch = await context.EmployeeImportBatches.SingleOrDefaultAsync(x => x.Id == options.BatchId, cancellationToken);
            if (batch is null) return Result<TerminationDateBackfillResult>.Failure("termination_date_backfill.batch_not_found", "The requested import batch does not exist.");
            var preview = _planner.Plan(batch.Id, batch.Status.ToString(), await LoadRowsAsync(context, batch.Id, cancellationToken), string.Empty, string.Empty);
            if (preview.CandidateCount != expectedCandidateCount) return Result<TerminationDateBackfillResult>.Failure("termination_date_backfill.candidate_count_changed", "Candidate count changed after confirmation; no update was applied.");
            var updated = 0;
            foreach (var candidate in preview.Candidates)
            {
                var affected = await context.Database.ExecuteSqlInterpolatedAsync($"UPDATE employees SET termination_date = {candidate.SourceTerminationDate} WHERE id = {candidate.EmployeeId} AND termination_date IS NULL AND {candidate.SourceTerminationDate} >= hire_date", cancellationToken);
                if (affected != 1) throw new InvalidOperationException("A backfill candidate no longer matched its safety predicate.");
                updated++;
            }
            await transaction.CommitAsync(cancellationToken);
            return Result<TerminationDateBackfillResult>.Success(new(updated, 0, 0, started, DateTime.UtcNow));
        }
        catch (OperationCanceledException) { await transaction.RollbackAsync(CancellationToken.None); throw; }
        catch { await transaction.RollbackAsync(CancellationToken.None); return Result<TerminationDateBackfillResult>.Failure("termination_date_backfill.transaction_failed", "Backfill transaction failed and was rolled back."); }
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    private HrDecisionSupportDbContext CreateContext() => new(
        new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector())
            .Options);
    private static async Task<List<TerminationDateBackfillRow>> LoadRowsAsync(HrDecisionSupportDbContext context, Guid batchId, CancellationToken cancellationToken)
    {
        var data = await (from row in context.EmployeeImportRows where row.ImportBatchId == batchId join employee in context.Employees on row.EmployeeId equals employee.Id into employees from employee in employees.DefaultIfEmpty() select new { row.Id, row.SourceRowNumber, row.EmployeeId, row.RawPayloadJson, HireDate = employee == null ? (DateOnly?)null : employee.HireDate, TerminationDate = employee == null ? (DateOnly?)null : employee.TerminationDate }).ToListAsync(cancellationToken);
        return data.Select(item => new TerminationDateBackfillRow(item.Id, item.SourceRowNumber, item.EmployeeId, item.HireDate, item.TerminationDate, RawTermination(item.RawPayloadJson))).ToList();
    }
    private static string? RawTermination(string json) { using var document = JsonDocument.Parse(json); return document.RootElement.TryGetProperty(EmployeeImportSpreadsheetHeaders.TerminationDate, out var value) ? value.GetString() : null; }
}

public static class TerminationDateBackfillReportWriter
{
    public static async Task WriteAsync(Guid batchId, string environment, int candidates, TerminationDateBackfillResult result, int secondRunCandidates, string postAuditDecision, CancellationToken cancellationToken)
    {
        var output = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HrDecisionSupport", "employee-import-remediation"); Directory.CreateDirectory(output);
        var report = new { batchId, environment, candidateCount = candidates, updatedCount = result.UpdatedCount, skippedCount = result.SkippedCount, failedCount = result.FailedCount, startedAtUtc = result.StartedAtUtc, completedAtUtc = result.CompletedAtUtc, status = "Completed", secondRunCandidateCount = secondRunCandidates, postAuditDecision };
        await File.WriteAllTextAsync(Path.Combine(output, "termination-date-backfill-result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
    }
}
