using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
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
