using System.Text;
using System.Text.Json;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace HrDecisionSupport.EmployeeImportDryRun;

public sealed record CompetencyBackfillOptions(Guid? BatchId, string? ConfirmCompetencies, string? ConfirmLinks, string EnvironmentName);
public sealed record CompetencyBackfillRawRow(Guid ImportRowId, Guid? PersonId, string RawPayloadJson, string? ValidationErrorsJson);
public sealed record CompetencyBackfillCandidate(Guid PersonId, string Code, string Name, HrDecisionSupport.Domain.Enums.CompetencyCategory Category);
public sealed record CompetencyBackfillPreview(Guid BatchId, string BatchStatus, int TotalImportRows, int DistinctSourceTokenCount, int ResolvedCanonicalTokenCount, int UnresolvedTokenCount, int AffectedDistinctPersons, int MatchingTokenOccurrences, int CandidateLinkCount, int ExistingLinkCount, int ExpectedNewCompetencyCount, int ExpectedReusedCompetencyCount, int ExpectedNewLinkCount, int DuplicateCandidateCount, int ConflictCount, string DatabaseHost, string DatabaseName, IReadOnlyList<string> UnresolvedTokens, IReadOnlyList<CompetencyBackfillCandidate> Candidates);
public sealed record CompetencyBackfillResult(Guid BatchId, int DistinctTokens, int AffectedPersons, int CandidateLinks, int CompetenciesCreated, int CompetenciesReused, int LinksCreated, int LinksAlreadyExisting, int UnresolvedTokens, int Conflicts, string Status, DateTime StartedAtUtc, DateTime CompletedAtUtc);

public interface ICompetencyBackfillExecution : IAsyncDisposable
{
    Task<Result<CompetencyBackfillPreview>> PreviewAsync(CompetencyBackfillOptions options, CancellationToken cancellationToken);
    Task<Result<CompetencyBackfillResult>> BackfillAsync(CompetencyBackfillOptions options, int expectedCompetencies, int expectedLinks, CancellationToken cancellationToken);
}
public interface ICompetencyBackfillConfirmation { Task<bool> ConfirmAsync(string requiredText, string? suppliedCompetencies, string? suppliedLinks, CancellationToken cancellationToken); }

public sealed class CompetencyBackfillPlanner(EmployeeImportRowNormalizer normalizer)
{
    public CompetencyBackfillPreview Plan(Guid batchId, string batchStatus, IEnumerable<CompetencyBackfillRawRow> rows, ISet<string> existingCodes, ISet<(Guid PersonId, string Code)> existingLinks, string host, string database)
    {
        var sourceTokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var unresolved = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var candidates = new List<CompetencyBackfillCandidate>(); var occurrences = 0; var conflicts = 0;
        foreach (var row in rows)
        {
            var (skills, tools) = RawSkills(row.RawPayloadJson); var historicalUnknown = HistoricalUnknownTokens(row.ValidationErrorsJson); var normalized = normalizer.NormalizeCompetencies(skills, tools);
            var selected = normalized.SourceTokens.Where(token => historicalUnknown.Contains(token)).Select(token => normalizer.NormalizeCompetencies(token, null)).ToArray();
            foreach (var token in normalized.SourceTokens.Where(token => historicalUnknown.Contains(token))) sourceTokens.Add(token);
            foreach (var token in selected.SelectMany(item => item.UnresolvedTokens)) unresolved.Add(token);
            occurrences += selected.Sum(item => item.ResolvedTokenOccurrences.Count);
            if (row.PersonId is null && selected.Any(item => item.ResolvedTokenOccurrences.Count > 0)) { conflicts++; continue; }
            if (row.PersonId is not null) candidates.AddRange(selected.SelectMany(item => item.ResolvedTokenOccurrences).Select(item => new CompetencyBackfillCandidate(row.PersonId.Value, item.Code, item.Name, item.Category)));
        }
        var canonicalByCode = candidates.GroupBy(candidate => candidate.Code, StringComparer.Ordinal).ToArray();
        conflicts += canonicalByCode.Count(group => group.Select(item => (item.Name, item.Category)).Distinct().Count() != 1);
        var distinctCandidates = candidates.GroupBy(candidate => (candidate.PersonId, candidate.Code)).Select(group => group.First()).ToArray();
        var duplicateCandidates = candidates.Count - distinctCandidates.Length;
        var codes = distinctCandidates.Select(candidate => candidate.Code).Distinct(StringComparer.Ordinal).ToArray();
        var existing = distinctCandidates.Count(candidate => existingLinks.Contains((candidate.PersonId, candidate.Code)));
        var missingCodes = codes.Count(code => !existingCodes.Contains(code));
        return new(batchId, batchStatus, rows.Count(), sourceTokens.Count, codes.Length, unresolved.Count, distinctCandidates.Select(candidate => candidate.PersonId).Distinct().Count(), occurrences, distinctCandidates.Length, existing, missingCodes, codes.Length - missingCodes, distinctCandidates.Length - existing, duplicateCandidates, conflicts, host, database, unresolved.OrderBy(token => token, StringComparer.OrdinalIgnoreCase).ToArray(), distinctCandidates);
    }

    private static (string? Skills, string? Tools) RawSkills(string rawJson)
    {
        using var document = JsonDocument.Parse(rawJson); var root = document.RootElement;
        return (Value(root, EmployeeImportSpreadsheetHeaders.TechnicalSkills, "TechnicalSkillsRaw"), Value(root, EmployeeImportSpreadsheetHeaders.TechnologiesAndTools, "TechnologiesAndToolsRaw"));
    }
    private static string? Value(JsonElement root, params string[] names) => names.FirstOrDefault(name => root.TryGetProperty(name, out _)) is { } name && root.TryGetProperty(name, out var value) ? value.GetString() : null;
    private static HashSet<string> HistoricalUnknownTokens(string? json)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase); if (string.IsNullOrWhiteSpace(json)) return result;
        using var document = JsonDocument.Parse(json); if (document.RootElement.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in document.RootElement.EnumerateArray()) if (String(item, "code", "Code") == "unknown_competency" && String(item, "propertyName", "PropertyName", "property", "Property") is { } token) result.Add(token); return result;
    }
    private static string? String(JsonElement item, params string[] names) => names.FirstOrDefault(name => item.TryGetProperty(name, out _)) is { } name && item.TryGetProperty(name, out var value) ? value.GetString() : null;
}

public sealed class CompetencyBackfillCommand(ICompetencyBackfillExecution execution, ICompetencyBackfillConfirmation confirmation, IImportOutput output)
{
    public async Task<int> ExecuteAsync(CompetencyBackfillOptions options, CancellationToken cancellationToken = default)
    {
        if (options.BatchId is null) return Fail("competency_backfill.batch_id_required", "--batch-id is required.");
        if (options.EnvironmentName.Equals("Production", StringComparison.OrdinalIgnoreCase)) return Fail("competency_backfill.production_locked", "Production backfill is locked.");
        if (!Allowed(options.EnvironmentName)) return Fail("competency_backfill.environment_not_allowed", "Target environment must be Development, Test, or Integration.");
        var preview = await execution.PreviewAsync(options, cancellationToken); if (preview.IsFailure) return Fail(preview.Error!.Code, preview.Error.Message);
        var value = preview.Value;
        output.WriteLine($"Competency backfill preview: batch={value.BatchId}; status={value.BatchStatus}; total-import-rows={value.TotalImportRows}; distinct-source-tokens={value.DistinctSourceTokenCount}; resolved-canonical-tokens={value.ResolvedCanonicalTokenCount}; unresolved={value.UnresolvedTokenCount}; affected-persons={value.AffectedDistinctPersons}; matching-occurrences={value.MatchingTokenOccurrences}; candidates={value.CandidateLinkCount}; existing-links={value.ExistingLinkCount}; new-competencies={value.ExpectedNewCompetencyCount}; reused-competencies={value.ExpectedReusedCompetencyCount}; new-links={value.ExpectedNewLinkCount}; duplicate-candidates={value.DuplicateCandidateCount}; conflicts={value.ConflictCount}; database={value.DatabaseHost}/{value.DatabaseName}.");
        await CompetencyBackfillReportWriter.WritePreviewAsync(value, cancellationToken);
        if (value.DistinctSourceTokenCount == 0) return Fail("competency_backfill.no_historical_tokens", "No historical unknown_competency tokens were found; confirmation is not available.");
        if (value.UnresolvedTokenCount != 0 || value.ConflictCount != 0) return Fail("competency_backfill.preview_blocked", "Unresolved tokens or conflicts block backfill.");
        var required = $"BACKFILL COMPETENCIES {value.ExpectedNewCompetencyCount} {value.ExpectedNewLinkCount}";
        if (!await confirmation.ConfirmAsync(required, options.ConfirmCompetencies, options.ConfirmLinks, cancellationToken)) return Fail("competency_backfill.confirmation_rejected", $"Confirmation is required. Type {required} to continue.");
        var result = await execution.BackfillAsync(options, value.ExpectedNewCompetencyCount, value.ExpectedNewLinkCount, cancellationToken); if (result.IsFailure) return Fail(result.Error!.Code, result.Error.Message);
        var second = await execution.PreviewAsync(options, cancellationToken); if (second.IsFailure) return Fail(second.Error!.Code, second.Error.Message);
        await CompetencyBackfillReportWriter.WriteResultAsync(result.Value, second.Value, cancellationToken);
        output.WriteLine($"Competency backfill completed: competencies-created={result.Value.CompetenciesCreated}; competencies-reused={result.Value.CompetenciesReused}; links-created={result.Value.LinksCreated}; links-existing={result.Value.LinksAlreadyExisting}; second-run-new-competencies={second.Value.ExpectedNewCompetencyCount}; second-run-new-links={second.Value.ExpectedNewLinkCount}.");
        return 0;
    }
    private int Fail(string code, string message) { output.WriteError($"{code}: {message}"); return 1; }
    private static bool Allowed(string environment) => environment.Equals("Development", StringComparison.OrdinalIgnoreCase) || environment.Equals("Test", StringComparison.OrdinalIgnoreCase) || environment.Equals("Integration", StringComparison.OrdinalIgnoreCase);
}

public sealed class ConsoleCompetencyBackfillConfirmation : ICompetencyBackfillConfirmation
{
    public Task<bool> ConfirmAsync(string requiredText, string? competencies, string? links, CancellationToken cancellationToken)
    {
        var counts = requiredText.Split(' ', StringSplitOptions.RemoveEmptyEntries); if (!string.IsNullOrWhiteSpace(competencies) || !string.IsNullOrWhiteSpace(links)) return Task.FromResult(competencies == counts[^2] && links == counts[^1]);
        if (Console.IsInputRedirected) return Task.FromResult(false); Console.Write($"Type {requiredText} to continue: "); return Task.FromResult(Console.ReadLine() == requiredText);
    }
}

public sealed class PostgreSqlCompetencyBackfillExecution(string? connectionString) : ICompetencyBackfillExecution
{
    private readonly CompetencyBackfillPlanner _planner = new(new EmployeeImportRowNormalizer());
    public async Task<Result<CompetencyBackfillPreview>> PreviewAsync(CompetencyBackfillOptions options, CancellationToken cancellationToken)
    {
        if (options.BatchId is null) return Result<CompetencyBackfillPreview>.Failure("competency_backfill.batch_id_required", "Batch id is required."); if (string.IsNullOrWhiteSpace(connectionString)) return Result<CompetencyBackfillPreview>.Failure("competency_backfill.connection_missing", "A PostgreSQL connection string is required.");
        try { var builder = new NpgsqlConnectionStringBuilder(connectionString); await using var context = CreateContext(); var batch = await context.EmployeeImportBatches.SingleOrDefaultAsync(item => item.Id == options.BatchId, cancellationToken); if (batch is null) return Result<CompetencyBackfillPreview>.Failure("competency_backfill.batch_not_found", "The requested import batch does not exist."); return Result<CompetencyBackfillPreview>.Success(await PlanAsync(context, batch.Id, batch.Status.ToString(), builder.Host ?? string.Empty, builder.Database ?? string.Empty, cancellationToken)); }
        catch (OperationCanceledException) { throw; } catch { return Result<CompetencyBackfillPreview>.Failure("competency_backfill.preview_failed", "Competency preview could not be completed."); }
    }
    public async Task<Result<CompetencyBackfillResult>> BackfillAsync(CompetencyBackfillOptions options, int expectedCompetencies, int expectedLinks, CancellationToken cancellationToken)
    {
        if (options.BatchId is null || string.IsNullOrWhiteSpace(connectionString)) return Result<CompetencyBackfillResult>.Failure("competency_backfill.preflight_required", "A batch id and connection are required.");
        var started = DateTime.UtcNow; await using var context = CreateContext(); await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var batch = await context.EmployeeImportBatches.SingleOrDefaultAsync(item => item.Id == options.BatchId, cancellationToken); if (batch is null) return Result<CompetencyBackfillResult>.Failure("competency_backfill.batch_not_found", "The requested import batch does not exist.");
            var preview = await PlanAsync(context, batch.Id, batch.Status.ToString(), "", "", cancellationToken);
            if (preview.UnresolvedTokenCount != 0 || preview.ConflictCount != 0) return Result<CompetencyBackfillResult>.Failure("competency_backfill.preview_blocked", "Unresolved tokens or conflicts block backfill.");
            if (preview.ExpectedNewCompetencyCount != expectedCompetencies || preview.ExpectedNewLinkCount != expectedLinks) return Result<CompetencyBackfillResult>.Failure("competency_backfill.count_changed", "Preview counts changed after confirmation; no update was applied.");
            var byCode = await context.Competencies.Where(item => preview.Candidates.Select(candidate => candidate.Code).Contains(item.Code)).ToDictionaryAsync(item => item.Code, StringComparer.Ordinal, cancellationToken); var created = 0;
            foreach (var candidate in preview.Candidates.GroupBy(item => item.Code, StringComparer.Ordinal).Select(group => group.First())) if (!byCode.ContainsKey(candidate.Code)) { var competency = new Competency { Id = Guid.NewGuid(), Code = candidate.Code, Name = candidate.Name, CompetencyCategory = candidate.Category, IsActive = true }; context.Competencies.Add(competency); byCode[candidate.Code] = competency; created++; }
            var candidatePersonIds = preview.Candidates.Select(candidate => candidate.PersonId).Distinct().ToArray();
            var existingPairs = (await context.PersonCompetencies.Where(link => candidatePersonIds.Contains(link.PersonId)).Select(link => new { link.PersonId, link.CompetencyId }).ToListAsync(cancellationToken)).Select(link => (link.PersonId, link.CompetencyId)).ToHashSet();
            var linksCreated = 0; foreach (var candidate in preview.Candidates) { var competencyId = byCode[candidate.Code].Id; if (!existingPairs.Add((candidate.PersonId, competencyId))) continue; context.PersonCompetencies.Add(new PersonCompetency { Id = Guid.NewGuid(), PersonId = candidate.PersonId, CompetencyId = competencyId }); linksCreated++; }
            await context.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken);
            return Result<CompetencyBackfillResult>.Success(new(batch.Id, preview.ResolvedCanonicalTokenCount, preview.AffectedDistinctPersons, preview.CandidateLinkCount, created, preview.ExpectedReusedCompetencyCount, linksCreated, preview.ExistingLinkCount, 0, 0, "Completed", started, DateTime.UtcNow));
        }
        catch (OperationCanceledException) { await transaction.RollbackAsync(CancellationToken.None); throw; } catch { await transaction.RollbackAsync(CancellationToken.None); return Result<CompetencyBackfillResult>.Failure("competency_backfill.transaction_failed", "Backfill transaction failed and was rolled back."); }
    }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    private HrDecisionSupportDbContext CreateContext() => new(
        new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseNpgsql(connectionString, npgsqlOptions => npgsqlOptions.UseVector())
            .Options);
    private async Task<CompetencyBackfillPreview> PlanAsync(HrDecisionSupportDbContext context, Guid batchId, string status, string host, string database, CancellationToken cancellationToken)
    {
        var rows = await context.EmployeeImportRows.Where(row => row.ImportBatchId == batchId).Select(row => new CompetencyBackfillRawRow(row.Id, row.Employee == null ? null : row.Employee.PersonId, row.RawPayloadJson, row.ValidationErrorsJson)).ToListAsync(cancellationToken);
        var existingCodes = (await context.Competencies.Select(item => item.Code).ToArrayAsync(cancellationToken)).ToHashSet(StringComparer.Ordinal);
        var persons = rows.Where(row => row.PersonId.HasValue).Select(row => row.PersonId!.Value).Distinct().ToArray(); var links = await (from link in context.PersonCompetencies where persons.Contains(link.PersonId) join competency in context.Competencies on link.CompetencyId equals competency.Id select new { link.PersonId, competency.Code }).ToListAsync(cancellationToken);
        return _planner.Plan(batchId, status, rows, existingCodes, links.Select(link => (link.PersonId, link.Code)).ToHashSet(), host, database);
    }
}

public static class CompetencyBackfillReportWriter
{
    private static string Output => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "HrDecisionSupport", "employee-competency-backfill");
    public static async Task WritePreviewAsync(CompetencyBackfillPreview preview, CancellationToken cancellationToken) { Directory.CreateDirectory(Output); await File.WriteAllTextAsync(Path.Combine(Output, "competency-backfill-preview.json"), JsonSerializer.Serialize(preview with { Candidates = [] }, new JsonSerializerOptions { WriteIndented = true }), cancellationToken); }
    public static async Task WriteResultAsync(CompetencyBackfillResult result, CompetencyBackfillPreview secondRun, CancellationToken cancellationToken) { Directory.CreateDirectory(Output); var report = new { batchId = result.BatchId, distinctTokens = result.DistinctTokens, affectedPersons = result.AffectedPersons, candidateLinks = result.CandidateLinks, competenciesCreated = result.CompetenciesCreated, competenciesReused = result.CompetenciesReused, linksCreated = result.LinksCreated, linksAlreadyExisting = result.LinksAlreadyExisting, unresolvedTokens = result.UnresolvedTokens, conflicts = result.Conflicts, status = result.Status, firstRun = result, secondRun = new { competenciesCreated = secondRun.ExpectedNewCompetencyCount, linksCreated = secondRun.ExpectedNewLinkCount }, startedAtUtc = result.StartedAtUtc, completedAtUtc = result.CompletedAtUtc }; await File.WriteAllTextAsync(Path.Combine(Output, "competency-backfill-result.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }), cancellationToken); await File.WriteAllTextAsync(Path.Combine(Output, "competency-backfill-summary.csv"), new StringBuilder("BatchId,DistinctTokens,AffectedPersons,CandidateLinks,CompetenciesCreated,LinksCreated\n").AppendLine($"{result.BatchId},{result.DistinctTokens},{result.AffectedPersons},{result.CandidateLinks},{result.CompetenciesCreated},{result.LinksCreated}").ToString(), cancellationToken); }
}

public sealed record CompetencyBackfillAuditOptions(Guid? BatchId, string? ConnectionString, string? ImportFilePath);
public sealed record CompetencyBackfillAuditResult(string Decision, int CanonicalCompetencyCount, int DistinctPersonCompetencyCount, int DuplicateLinkCount, int OrphanCount, int MissingExpectedLinkCount, int BatchRows, int HistoricalDiagnosticRows, int HistoricalTokenCount, int ResolvedTokenCount, int UnresolvedTokenCount, int AffectedPersons, int CandidateLinks, int SecondRunNewCompetencies, int SecondRunNewLinks, int EmployeeCount, int PersonCount, int SnapshotCount, int AssignmentCount, int LabelCount, int BatchTotalRows, int BatchSuccessfulRows, int BatchFailedRows, bool DiagnosticsRetained, int? DryRunValidRows, int? DryRunWarningRows, int? DryRunInvalidRows);

public sealed class CompetencyBackfillAuditCommand(PostgreSqlCompetencyBackfillAuditExecution execution, IImportOutput output)
{
    public async Task<int> ExecuteAsync(CompetencyBackfillAuditOptions options, CancellationToken cancellationToken = default)
    {
        var result = await execution.AuditAsync(options, cancellationToken); if (result.IsFailure) { output.WriteError($"{result.Error!.Code}: {result.Error.Message}"); return 1; }
        var value = result.Value; output.WriteLine($"Competency backfill audit: canonical-competencies={value.CanonicalCompetencyCount}; distinct-links={value.DistinctPersonCompetencyCount}; duplicate-links={value.DuplicateLinkCount}; orphans={value.OrphanCount}; missing-links={value.MissingExpectedLinkCount}; historical-tokens={value.HistoricalTokenCount}; resolved={value.ResolvedTokenCount}; unresolved={value.UnresolvedTokenCount}; affected-persons={value.AffectedPersons}; candidates={value.CandidateLinks}; second-run-new-competencies={value.SecondRunNewCompetencies}; second-run-new-links={value.SecondRunNewLinks}; dry-run-valid={value.DryRunValidRows}; dry-run-warnings={value.DryRunWarningRows}; dry-run-invalid={value.DryRunInvalidRows}; decision={value.Decision}."); return value.Decision == "COMPETENCY_BACKFILL_VERIFIED" ? 0 : 1;
    }
}

public sealed class PostgreSqlCompetencyBackfillAuditExecution
{
    public async Task<Result<CompetencyBackfillAuditResult>> AuditAsync(CompetencyBackfillAuditOptions options, CancellationToken cancellationToken)
    {
        if (options.BatchId is null) return Result<CompetencyBackfillAuditResult>.Failure("competency_backfill_audit.batch_id_required", "--batch-id is required."); if (string.IsNullOrWhiteSpace(options.ConnectionString)) return Result<CompetencyBackfillAuditResult>.Failure("competency_backfill_audit.connection_missing", "A PostgreSQL connection string is required.");
        var stage = "open";
        try
        {
            await using var context = new HrDecisionSupportDbContext(new DbContextOptionsBuilder<HrDecisionSupportDbContext>().UseNpgsql(options.ConnectionString, npgsqlOptions => npgsqlOptions.UseVector()).Options); stage = "batch"; var batch = await context.EmployeeImportBatches.SingleOrDefaultAsync(item => item.Id == options.BatchId, cancellationToken); if (batch is null) return Result<CompetencyBackfillAuditResult>.Failure("competency_backfill_audit.batch_not_found", "The requested import batch does not exist.");
            stage = "historical-preview"; var preview = await new PostgreSqlCompetencyBackfillExecution(options.ConnectionString).PreviewAsync(new(options.BatchId, null, null, "Development"), cancellationToken); if (preview.IsFailure) return Result<CompetencyBackfillAuditResult>.Failure(preview.Error!); var plan = preview.Value;
            var candidates = plan.Candidates.Select(item => (item.PersonId, item.Code)).ToHashSet(); var personIds = candidates.Select(item => item.PersonId).Distinct().ToArray(); var codes = candidates.Select(item => item.Code).Distinct().ToArray();
            stage = "expected-links"; var actual = await (from link in context.PersonCompetencies join competency in context.Competencies on link.CompetencyId equals competency.Id where personIds.Contains(link.PersonId) && codes.Contains(competency.Code) select new { link.PersonId, competency.Code }).ToListAsync(cancellationToken); var actualPairs = actual.Select(item => (item.PersonId, item.Code)).ToHashSet();
            stage = "integrity"; var missing = candidates.Count(candidate => !actualPairs.Contains(candidate)); var duplicate = await context.PersonCompetencies.GroupBy(link => new { link.PersonId, link.CompetencyId }).Where(group => group.Count() > 1).Select(group => group.Count() - 1).DefaultIfEmpty().SumAsync(cancellationToken); var orphan = await context.PersonCompetencies.CountAsync(link => !context.People.Any(person => person.Id == link.PersonId) || !context.Competencies.Any(competency => competency.Id == link.CompetencyId), cancellationToken);
            stage = "dry-run"; int? valid = null, warnings = null, invalid = null; if (!string.IsNullOrWhiteSpace(options.ImportFilePath) && File.Exists(options.ImportFilePath)) { var dryRun = new EmployeeImportDryRunService(new HrDecisionSupport.Infrastructure.EmployeeImports.ClosedXmlEmployeeSpreadsheetReader(), new EmployeeImportRowNormalizer(), new EmployeeImportRowValidator()); await using var stream = File.OpenRead(options.ImportFilePath); var run = await dryRun.DryRunAsync(stream, new(HrDecisionSupport.Domain.Enums.EmployeeDatasetSplit.Training, new DateOnly(2026, 8, 6)), cancellationToken); if (run.IsSuccess) { valid = run.Value.ValidRows; warnings = run.Value.ValidWithWarningsRows; invalid = run.Value.InvalidRows; } }
            var decision = batch.Status.ToString() == "Completed" && plan.HistoricalTokenCount() > 0 && plan.UnresolvedTokenCount == 0 && missing == 0 && orphan == 0 && plan.ExpectedNewCompetencyCount == 0 && plan.ExpectedNewLinkCount == 0 && valid is 6800 && warnings == 0 && invalid == 0 ? (duplicate == 0 ? "COMPETENCY_BACKFILL_VERIFIED" : "COMPETENCY_BACKFILL_VERIFIED_WITH_DUPLICATES") : "COMPETENCY_BACKFILL_DATA_MISMATCH";
            return Result<CompetencyBackfillAuditResult>.Success(new(decision, await context.Competencies.CountAsync(item => codes.Contains(item.Code), cancellationToken), actualPairs.Count, duplicate, orphan, missing, plan.TotalImportRows, await context.EmployeeImportRows.CountAsync(row => row.ImportBatchId == options.BatchId && row.ValidationErrorsJson != null, cancellationToken), plan.DistinctSourceTokenCount, plan.ResolvedCanonicalTokenCount, plan.UnresolvedTokenCount, plan.AffectedDistinctPersons, plan.CandidateLinkCount, plan.ExpectedNewCompetencyCount, plan.ExpectedNewLinkCount, await context.Employees.CountAsync(cancellationToken), await context.People.CountAsync(cancellationToken), await context.EmployeeCareerFeatureSnapshots.CountAsync(cancellationToken), await context.EmployeeAssignments.CountAsync(cancellationToken), await context.EmployeeRetentionLabels.CountAsync(cancellationToken), batch.TotalRowCount, batch.SuccessfulRowCount, batch.FailedRowCount, await context.EmployeeImportRows.AnyAsync(row => row.ImportBatchId == options.BatchId && row.ValidationErrorsJson != null, cancellationToken), valid, warnings, invalid));
        }
        catch (OperationCanceledException) { throw; } catch (Exception exception) { return Result<CompetencyBackfillAuditResult>.Failure("competency_backfill_audit.failed", $"Read-only competency backfill audit failed during {stage}: {exception.GetType().Name}."); }
    }
}

internal static class CompetencyBackfillAuditExtensions { public static int HistoricalTokenCount(this CompetencyBackfillPreview preview) => preview.DistinctSourceTokenCount; }
