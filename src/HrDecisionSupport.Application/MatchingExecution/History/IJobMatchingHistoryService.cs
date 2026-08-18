using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.MatchingExecution.History;

public interface IJobMatchingHistoryService
{
    Task<IReadOnlyList<JobMatchingRunListItem>> GetRunsForJobAsync(
        Guid jobRequisitionId,
        CancellationToken cancellationToken = default);

    Task<JobMatchingRunDetail?> GetRunDetailAsync(
        Guid jobRequisitionId,
        Guid runId,
        CancellationToken cancellationToken = default);

    Task<JobMatchingRunComparison?> GetComparisonAsync(
        Guid jobRequisitionId,
        Guid runAId,
        Guid runBId,
        CancellationToken cancellationToken = default);

    Task SaveCompletedRunAsync(
        CompletedJobMatchingRun completedRun,
        CancellationToken cancellationToken = default);
}

public sealed record CompletedJobMatchingRun(
    Guid JobRequisitionId,
    int RetrievalTopN,
    int FinalTopN,
    int CandidatePoolCount,
    int HardFilterPassedCount,
    int RetrievedCandidateCount,
    int FinalCandidateCount,
    string JobDocumentText,
    IReadOnlyList<CompletedJobMatchingResult> Results);

public sealed record CompletedJobMatchingResult(
    Guid CandidateId,
    string? CandidateCodeSnapshot,
    string CandidateDisplayNameSnapshot,
    int FinalRank,
    int SkillTier,
    decimal MandatorySkillCoverage,
    decimal PreferredSkillCoverage,
    double EmbeddingScore,
    double CrossEncoderRawScore,
    double JobFitScore,
    RetentionPredictionStatus RetentionPredictionStatus,
    EmployeeRetentionLabelValue? RetentionLabel,
    int? ShortestPreviousJobMonthsSnapshot,
    int? LongestPreviousJobMonthsSnapshot);
