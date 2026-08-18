using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.MatchingExecution.History;

public interface IJobMatchingHistoryService
{
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
