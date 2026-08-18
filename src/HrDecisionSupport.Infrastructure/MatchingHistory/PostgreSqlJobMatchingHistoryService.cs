using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Globalization;
using HrDecisionSupport.Application.MatchingExecution.History;
using HrDecisionSupport.Application.PreScreening.Policy;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using HrDecisionSupport.Infrastructure.SemanticMatching;
using HrDecisionSupport.Infrastructure.SemanticMatching.Reranking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HrDecisionSupport.Infrastructure.MatchingHistory;

public sealed class PostgreSqlJobMatchingHistoryService : IJobMatchingHistoryService
{
    internal const string RetentionModelName = "retention-rf-v1";
    internal const string RetentionFeatureSchemaVersion = "retention-features-v1";
    internal const string ConfigurationSnapshotSchemaVersion = "job-matching-configuration-v1";

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly HrDecisionSupportDbContext _dbContext;
    private readonly QwenEmbeddingOptions _embeddingOptions;
    private readonly QwenRerankerOptions _rerankerOptions;
    private readonly TimeProvider _timeProvider;

    public PostgreSqlJobMatchingHistoryService(
        HrDecisionSupportDbContext dbContext,
        IOptions<QwenEmbeddingOptions> embeddingOptions,
        IOptions<QwenRerankerOptions> rerankerOptions,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _embeddingOptions = embeddingOptions.Value;
        _rerankerOptions = rerankerOptions.Value;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<JobMatchingRunListItem>> GetRunsForJobAsync(
        Guid jobRequisitionId,
        CancellationToken cancellationToken = default) =>
        await _dbContext.JobMatchingRuns
            .AsNoTracking()
            .Where(run => run.JobRequisitionId == jobRequisitionId)
            .OrderByDescending(run => run.ExecutedAtUtc)
            .Select(run => new JobMatchingRunListItem(
                run.Id,
                run.ExecutedAtUtc,
                run.JobRequisitionCodeSnapshot,
                run.JobTitleSnapshot,
                run.RetrievalTopN,
                run.FinalTopN,
                run.FinalCandidateCount,
                run.EmbeddingModelName,
                run.RerankerModelName,
                run.RetentionModelName))
            .ToListAsync(cancellationToken);

    public async Task<JobMatchingRunDetail?> GetRunDetailAsync(
        Guid jobRequisitionId,
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        var run = await _dbContext.JobMatchingRuns
            .AsNoTracking()
            .Include(item => item.Results)
            .SingleOrDefaultAsync(
                item => item.Id == runId && item.JobRequisitionId == jobRequisitionId,
                cancellationToken);

        if (run is null)
        {
            return null;
        }

        var candidates = run.Results
            .OrderBy(result => result.FinalRank)
            .Select(result => new JobMatchingHistoricalCandidate(
                result.CandidateId,
                result.CandidateCodeSnapshot,
                result.CandidateDisplayNameSnapshot,
                result.FinalRank,
                result.SkillTier,
                result.MandatorySkillCoverage,
                result.PreferredSkillCoverage,
                result.EmbeddingScore,
                result.CrossEncoderRawScore,
                result.JobFitScore,
                result.RetentionPredictionStatus,
                result.RetentionLabel,
                result.ShortestPreviousJobMonthsSnapshot,
                result.LongestPreviousJobMonthsSnapshot))
            .ToList();

        return new JobMatchingRunDetail(
            run.Id,
            run.ExecutedAtUtc,
            run.JobRequisitionCodeSnapshot,
            run.JobTitleSnapshot,
            run.RetrievalTopN,
            run.FinalTopN,
            run.CandidatePoolCount,
            run.HardFilterPassedCount,
            run.RetrievedCandidateCount,
            run.FinalCandidateCount,
            run.EmbeddingModelName,
            run.RerankerModelName,
            run.RetentionModelName,
            run.RetentionFeatureSchemaVersion,
            run.JobDocumentHash,
            run.ConfigurationSnapshotJson,
            TryDeserializeConfiguration(run.ConfigurationSnapshotJson),
            candidates);
    }

    public async Task<JobMatchingRunComparison?> GetComparisonAsync(
        Guid jobRequisitionId,
        Guid runAId,
        Guid runBId,
        CancellationToken cancellationToken = default)
    {
        if (runAId == runBId)
        {
            return null;
        }

        var runIds = new[] { runAId, runBId };
        var runs = await _dbContext.JobMatchingRuns
            .AsNoTracking()
            .Where(run => run.JobRequisitionId == jobRequisitionId && runIds.Contains(run.Id))
            .Include(run => run.Results)
            .ToListAsync(cancellationToken);

        if (runs.Count != 2)
        {
            return null;
        }

        var runA = runs.Single(run => run.Id == runAId);
        var runB = runs.Single(run => run.Id == runBId);
        var resultsA = runA.Results.ToDictionary(result => result.CandidateId);
        var resultsB = runB.Results.ToDictionary(result => result.CandidateId);

        var candidates = runB.Results
            .OrderBy(result => result.FinalRank)
            .Select(resultB => BuildCandidateComparison(
                resultsA.GetValueOrDefault(resultB.CandidateId),
                resultB))
            .Concat(runA.Results
                .Where(resultA => !resultsB.ContainsKey(resultA.CandidateId))
                .OrderBy(resultA => resultA.FinalRank)
                .Select(resultA => BuildCandidateComparison(resultA, null)))
            .ToList();

        var configurationA = TryDeserializeConfiguration(runA.ConfigurationSnapshotJson);
        var configurationB = TryDeserializeConfiguration(runB.ConfigurationSnapshotJson);

        return new JobMatchingRunComparison(
            ToComparisonRun(runA),
            ToComparisonRun(runB),
            ModelConfigurationChanged(runA, runB),
            !string.Equals(runA.JobDocumentHash, runB.JobDocumentHash, StringComparison.Ordinal),
            CompareConfigurations(configurationA, configurationB),
            new JobMatchingComparisonSummary(
                PresentInBoth: candidates.Count(candidate =>
                    candidate.MovementStatus is CandidateMovementStatus.Up
                        or CandidateMovementStatus.Down
                        or CandidateMovementStatus.Unchanged),
                New: candidates.Count(candidate => candidate.MovementStatus == CandidateMovementStatus.New),
                Dropped: candidates.Count(candidate => candidate.MovementStatus == CandidateMovementStatus.Dropped),
                MovedUp: candidates.Count(candidate => candidate.MovementStatus == CandidateMovementStatus.Up),
                MovedDown: candidates.Count(candidate => candidate.MovementStatus == CandidateMovementStatus.Down),
                Unchanged: candidates.Count(candidate => candidate.MovementStatus == CandidateMovementStatus.Unchanged)),
            candidates);
    }

    public async Task SaveCompletedRunAsync(
        CompletedJobMatchingRun completedRun,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(completedRun);

        var job = await _dbContext.JobRequisitions
            .AsNoTracking()
            .Include(requisition => requisition.Position)
            .Include(requisition => requisition.WorkMode)
            .Include(requisition => requisition.Requirements)
                .ThenInclude(requirement => requirement.Competency)
            .Include(requisition => requisition.LanguageRequirements)
                .ThenInclude(requirement => requirement.Language)
            .SingleAsync(
                requisition => requisition.Id == completedRun.JobRequisitionId,
                cancellationToken);

        var runId = Guid.NewGuid();
        var run = new JobMatchingRun
        {
            Id = runId,
            JobRequisitionId = job.Id,
            ExecutedAtUtc = _timeProvider.GetUtcNow().UtcDateTime,
            JobRequisitionCodeSnapshot = job.RequisitionCode,
            JobTitleSnapshot = job.Title,
            RetrievalTopN = completedRun.RetrievalTopN,
            FinalTopN = completedRun.FinalTopN,
            CandidatePoolCount = completedRun.CandidatePoolCount,
            HardFilterPassedCount = completedRun.HardFilterPassedCount,
            RetrievedCandidateCount = completedRun.RetrievedCandidateCount,
            FinalCandidateCount = completedRun.FinalCandidateCount,
            EmbeddingModelName = _embeddingOptions.ModelName,
            RerankerModelName = _rerankerOptions.ModelName,
            RetentionModelName = RetentionModelName,
            RetentionFeatureSchemaVersion = RetentionFeatureSchemaVersion,
            JobDocumentHash = ComputeDocumentHash(completedRun.JobDocumentText),
            ConfigurationSnapshotJson = SerializeConfiguration(job),
            Results = completedRun.Results.Select(result => new JobMatchingRunResult
            {
                JobMatchingRunId = runId,
                CandidateId = result.CandidateId,
                CandidateCodeSnapshot = result.CandidateCodeSnapshot,
                CandidateDisplayNameSnapshot = result.CandidateDisplayNameSnapshot,
                FinalRank = result.FinalRank,
                SkillTier = result.SkillTier,
                MandatorySkillCoverage = result.MandatorySkillCoverage,
                PreferredSkillCoverage = result.PreferredSkillCoverage,
                EmbeddingScore = result.EmbeddingScore,
                CrossEncoderRawScore = result.CrossEncoderRawScore,
                JobFitScore = result.JobFitScore,
                RetentionPredictionStatus = result.RetentionPredictionStatus,
                RetentionLabel = result.RetentionLabel,
                ShortestPreviousJobMonthsSnapshot = result.ShortestPreviousJobMonthsSnapshot,
                LongestPreviousJobMonthsSnapshot = result.LongestPreviousJobMonthsSnapshot
            }).ToList()
        };

        _dbContext.JobMatchingRuns.Add(run);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string SerializeConfiguration(JobRequisition job)
    {
        var policy = new CandidatePreScreeningPolicy();
        var snapshot = new JobMatchingConfigurationSnapshot(
            ConfigurationSnapshotSchemaVersion,
            job.Title,
            job.Description,
            new ConfigurationReference(job.Position.Id, job.Position.Code, job.Position.Name),
            job.MinimumRelevantExperienceMonths,
            job.MinimumEducationLevel,
            job.MandatorySkillCoverageThreshold ?? policy.MandatorySkillCoverageThreshold,
            job.WorkMode is null
                ? null
                : new ConfigurationReference(job.WorkMode.Id, job.WorkMode.Code, job.WorkMode.Name),
            job.WorkModeHardFilterEnabled,
            job.Requirements
                .Where(requirement => requirement.IsRequired)
                .OrderBy(requirement => requirement.Competency.Name, StringComparer.Ordinal)
                .ThenBy(requirement => requirement.CompetencyId)
                .Select(ToCompetencySnapshot)
                .ToList(),
            job.Requirements
                .Where(requirement => !requirement.IsRequired)
                .OrderBy(requirement => requirement.Competency.Name, StringComparer.Ordinal)
                .ThenBy(requirement => requirement.CompetencyId)
                .Select(ToCompetencySnapshot)
                .ToList(),
            job.LanguageRequirements
                .OrderBy(requirement => requirement.Language.Name, StringComparer.Ordinal)
                .ThenBy(requirement => requirement.LanguageId)
                .Select(requirement => new LanguageRequirementSnapshot(
                    requirement.LanguageId,
                    requirement.Language.Code,
                    requirement.Language.Name,
                    requirement.MinimumProficiency,
                    requirement.HardFilterEnabled))
                .ToList());

        return JsonSerializer.Serialize(snapshot, SnapshotJsonOptions);
    }

    private static CompetencyRequirementSnapshot ToCompetencySnapshot(
        JobRequisitionRequirement requirement) =>
        new(
            requirement.CompetencyId,
            requirement.Competency.Code,
            requirement.Competency.Name);

    private static string ComputeDocumentHash(string document) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document))).ToLowerInvariant();

    private static JobMatchingComparisonRun ToComparisonRun(JobMatchingRun run) =>
        new(
            run.Id,
            run.ExecutedAtUtc,
            run.JobRequisitionCodeSnapshot,
            run.JobTitleSnapshot,
            run.RetrievalTopN,
            run.FinalTopN,
            run.CandidatePoolCount,
            run.HardFilterPassedCount,
            run.RetrievedCandidateCount,
            run.FinalCandidateCount,
            run.EmbeddingModelName,
            run.RerankerModelName,
            run.RetentionModelName,
            run.RetentionFeatureSchemaVersion,
            run.JobDocumentHash);

    private static JobMatchingCandidateComparison BuildCandidateComparison(
        JobMatchingRunResult? resultA,
        JobMatchingRunResult? resultB)
    {
        int? movement = resultA is not null && resultB is not null
            ? resultA.FinalRank - resultB.FinalRank
            : null;
        var movementStatus = (resultA, resultB, movement) switch
        {
            (null, not null, _) => CandidateMovementStatus.New,
            (not null, null, _) => CandidateMovementStatus.Dropped,
            (_, _, > 0) => CandidateMovementStatus.Up,
            (_, _, < 0) => CandidateMovementStatus.Down,
            _ => CandidateMovementStatus.Unchanged
        };
        var displayResult = resultB ?? resultA!;

        return new JobMatchingCandidateComparison(
            displayResult.CandidateId,
            displayResult.CandidateCodeSnapshot,
            displayResult.CandidateDisplayNameSnapshot,
            resultA?.FinalRank,
            resultB?.FinalRank,
            movement,
            movementStatus,
            resultA?.SkillTier,
            resultB?.SkillTier,
            resultA?.MandatorySkillCoverage,
            resultB?.MandatorySkillCoverage,
            resultA?.PreferredSkillCoverage,
            resultB?.PreferredSkillCoverage,
            resultA?.EmbeddingScore,
            resultB?.EmbeddingScore,
            resultA?.CrossEncoderRawScore,
            resultB?.CrossEncoderRawScore,
            resultA?.JobFitScore,
            resultB?.JobFitScore,
            resultA?.RetentionPredictionStatus,
            resultB?.RetentionPredictionStatus,
            resultA?.RetentionLabel,
            resultB?.RetentionLabel);
    }

    private static bool ModelConfigurationChanged(JobMatchingRun runA, JobMatchingRun runB) =>
        !string.Equals(runA.EmbeddingModelName, runB.EmbeddingModelName, StringComparison.Ordinal)
        || !string.Equals(runA.RerankerModelName, runB.RerankerModelName, StringComparison.Ordinal)
        || !string.Equals(runA.RetentionModelName, runB.RetentionModelName, StringComparison.Ordinal)
        || !string.Equals(
            runA.RetentionFeatureSchemaVersion,
            runB.RetentionFeatureSchemaVersion,
            StringComparison.Ordinal);

    private static JobMatchingConfigurationComparison CompareConfigurations(
        global::HrDecisionSupport.Application.MatchingExecution.History.JobMatchingConfigurationSnapshot? configurationA,
        global::HrDecisionSupport.Application.MatchingExecution.History.JobMatchingConfigurationSnapshot? configurationB)
    {
        if (configurationA is null || configurationB is null)
        {
            return new(false, null, []);
        }

        var differences = new List<JobMatchingConfigurationDifference>();
        AddDifference(differences, "Title", configurationA.JobTitle, configurationB.JobTitle);
        AddDifference(differences, "Description", configurationA.JobDescription, configurationB.JobDescription);
        AddDifference(
            differences,
            "Position",
            configurationA.Position,
            configurationB.Position,
            reference => reference is null ? null : $"{reference.Name} ({reference.Code})");
        AddDifference(
            differences,
            "Minimum experience",
            configurationA.MinimumRelevantExperienceMonths,
            configurationB.MinimumRelevantExperienceMonths,
            value => value?.ToString(CultureInfo.InvariantCulture));
        AddDifference(
            differences,
            "Minimum education",
            configurationA.MinimumEducation,
            configurationB.MinimumEducation,
            value => value?.ToString());
        AddDifference(
            differences,
            "Mandatory skill threshold",
            configurationA.MandatorySkillCoverageThreshold,
            configurationB.MandatorySkillCoverageThreshold,
            value => value.ToString(CultureInfo.InvariantCulture));
        AddDifference(
            differences,
            "Work mode",
            configurationA.WorkMode,
            configurationB.WorkMode,
            reference => reference is null ? null : $"{reference.Name} ({reference.Code})");
        AddDifference(
            differences,
            "Work mode hard filter",
            configurationA.WorkModeHardFilterEnabled,
            configurationB.WorkModeHardFilterEnabled,
            value => value.ToString());
        AddSequenceDifference(
            differences,
            "Mandatory competencies",
            configurationA.MandatoryCompetencies.OrderBy(item => item.CompetencyId),
            configurationB.MandatoryCompetencies.OrderBy(item => item.CompetencyId),
            items => string.Join(", ", items.Select(item => item.Name)));
        AddSequenceDifference(
            differences,
            "Preferred competencies",
            configurationA.PreferredCompetencies.OrderBy(item => item.CompetencyId),
            configurationB.PreferredCompetencies.OrderBy(item => item.CompetencyId),
            items => string.Join(", ", items.Select(item => item.Name)));
        AddSequenceDifference(
            differences,
            "Language requirements",
            configurationA.LanguageRequirements.OrderBy(item => item.LanguageId),
            configurationB.LanguageRequirements.OrderBy(item => item.LanguageId),
            items => string.Join(", ", items.Select(item =>
                $"{item.Name} {item.MinimumProficiency}"
                + (item.HardFilterEnabled ? " [hard]" : string.Empty))));

        return new(true, differences.Count == 0, differences);
    }

    private static void AddDifference<T>(
        ICollection<JobMatchingConfigurationDifference> differences,
        string field,
        T valueA,
        T valueB,
        Func<T, string?>? format = null)
    {
        if (EqualityComparer<T>.Default.Equals(valueA, valueB))
        {
            return;
        }

        format ??= value => value?.ToString();
        differences.Add(new(field, format(valueA), format(valueB)));
    }

    private static void AddSequenceDifference<T>(
        ICollection<JobMatchingConfigurationDifference> differences,
        string field,
        IEnumerable<T> valuesA,
        IEnumerable<T> valuesB,
        Func<IReadOnlyList<T>, string> format)
    {
        var listA = valuesA.ToList();
        var listB = valuesB.ToList();
        if (listA.SequenceEqual(listB))
        {
            return;
        }

        differences.Add(new(field, format(listA), format(listB)));
    }

    private static global::HrDecisionSupport.Application.MatchingExecution.History.JobMatchingConfigurationSnapshot?
        TryDeserializeConfiguration(string json)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<
                global::HrDecisionSupport.Application.MatchingExecution.History.JobMatchingConfigurationSnapshot>(
                json,
                SnapshotJsonOptions);

            return snapshot is not null
                && snapshot.SnapshotSchemaVersion == ConfigurationSnapshotSchemaVersion
                && snapshot.Position is not null
                && snapshot.MandatoryCompetencies is not null
                && snapshot.MandatoryCompetencies.All(item => item is not null)
                && snapshot.PreferredCompetencies is not null
                && snapshot.PreferredCompetencies.All(item => item is not null)
                && snapshot.LanguageRequirements is not null
                && snapshot.LanguageRequirements.All(item => item is not null)
                    ? snapshot
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private sealed record JobMatchingConfigurationSnapshot(
        string SnapshotSchemaVersion,
        string JobTitle,
        string? JobDescription,
        ConfigurationReference Position,
        int? MinimumRelevantExperienceMonths,
        DegreeLevel? MinimumEducation,
        decimal MandatorySkillCoverageThreshold,
        ConfigurationReference? WorkMode,
        bool WorkModeHardFilterEnabled,
        IReadOnlyList<CompetencyRequirementSnapshot> MandatoryCompetencies,
        IReadOnlyList<CompetencyRequirementSnapshot> PreferredCompetencies,
        IReadOnlyList<LanguageRequirementSnapshot> LanguageRequirements);

    private sealed record ConfigurationReference(Guid Id, string Code, string Name);

    private sealed record CompetencyRequirementSnapshot(
        Guid CompetencyId,
        string Code,
        string Name);

    private sealed record LanguageRequirementSnapshot(
        Guid LanguageId,
        string Code,
        string Name,
        LanguageProficiencyLevel MinimumProficiency,
        bool HardFilterEnabled);
}
