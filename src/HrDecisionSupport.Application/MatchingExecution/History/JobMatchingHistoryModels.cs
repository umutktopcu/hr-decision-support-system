using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.MatchingExecution.History;

public sealed record JobMatchingRunListItem(
    Guid RunId,
    DateTime ExecutedAtUtc,
    string JobRequisitionCodeSnapshot,
    string JobTitleSnapshot,
    int RetrievalTopN,
    int FinalTopN,
    int FinalCandidateCount,
    string EmbeddingModelName,
    string RerankerModelName,
    string RetentionModelName);

public sealed record JobMatchingRunDetail(
    Guid RunId,
    DateTime ExecutedAtUtc,
    string JobRequisitionCodeSnapshot,
    string JobTitleSnapshot,
    int RetrievalTopN,
    int FinalTopN,
    int CandidatePoolCount,
    int HardFilterPassedCount,
    int RetrievedCandidateCount,
    int FinalCandidateCount,
    string EmbeddingModelName,
    string RerankerModelName,
    string RetentionModelName,
    string RetentionFeatureSchemaVersion,
    string JobDocumentHash,
    string ConfigurationSnapshotJson,
    JobMatchingConfigurationSnapshot? Configuration,
    IReadOnlyList<JobMatchingHistoricalCandidate> Candidates);

public sealed record JobMatchingHistoricalCandidate(
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

public sealed record JobMatchingConfigurationSnapshot(
    string SnapshotSchemaVersion,
    string JobTitle,
    string? JobDescription,
    MatchingConfigurationReference Position,
    int? MinimumRelevantExperienceMonths,
    DegreeLevel? MinimumEducation,
    decimal MandatorySkillCoverageThreshold,
    MatchingConfigurationReference? WorkMode,
    bool WorkModeHardFilterEnabled,
    IReadOnlyList<MatchingCompetencySnapshot> MandatoryCompetencies,
    IReadOnlyList<MatchingCompetencySnapshot> PreferredCompetencies,
    IReadOnlyList<MatchingLanguageRequirementSnapshot> LanguageRequirements);

public sealed record MatchingConfigurationReference(Guid Id, string Code, string Name);

public sealed record MatchingCompetencySnapshot(Guid CompetencyId, string Code, string Name);

public sealed record MatchingLanguageRequirementSnapshot(
    Guid LanguageId,
    string Code,
    string Name,
    LanguageProficiencyLevel MinimumProficiency,
    bool HardFilterEnabled);

public enum CandidateMovementStatus
{
    Up = 1,
    Down = 2,
    Unchanged = 3,
    New = 4,
    Dropped = 5
}

public sealed record JobMatchingRunComparison(
    JobMatchingComparisonRun RunA,
    JobMatchingComparisonRun RunB,
    bool ModelConfigurationChanged,
    bool SemanticJobDocumentChanged,
    JobMatchingConfigurationComparison ConfigurationComparison,
    JobMatchingComparisonSummary Summary,
    IReadOnlyList<JobMatchingCandidateComparison> Candidates);

public sealed record JobMatchingComparisonRun(
    Guid RunId,
    DateTime ExecutedAtUtc,
    string JobRequisitionCodeSnapshot,
    string JobTitleSnapshot,
    int RetrievalTopN,
    int FinalTopN,
    int CandidatePoolCount,
    int HardFilterPassedCount,
    int RetrievedCandidateCount,
    int FinalCandidateCount,
    string EmbeddingModelName,
    string RerankerModelName,
    string RetentionModelName,
    string RetentionFeatureSchemaVersion,
    string JobDocumentHash);

public sealed record JobMatchingCandidateComparison(
    Guid CandidateId,
    string? CandidateCodeSnapshot,
    string CandidateDisplayNameSnapshot,
    int? RankA,
    int? RankB,
    int? Movement,
    CandidateMovementStatus MovementStatus,
    int? SkillTierA,
    int? SkillTierB,
    decimal? MandatoryCoverageA,
    decimal? MandatoryCoverageB,
    decimal? PreferredCoverageA,
    decimal? PreferredCoverageB,
    double? EmbeddingScoreA,
    double? EmbeddingScoreB,
    double? CrossEncoderRawScoreA,
    double? CrossEncoderRawScoreB,
    double? JobFitScoreA,
    double? JobFitScoreB,
    RetentionPredictionStatus? RetentionStatusA,
    RetentionPredictionStatus? RetentionStatusB,
    EmployeeRetentionLabelValue? RetentionLabelA,
    EmployeeRetentionLabelValue? RetentionLabelB);

public sealed record JobMatchingComparisonSummary(
    int PresentInBoth,
    int New,
    int Dropped,
    int MovedUp,
    int MovedDown,
    int Unchanged);

public sealed record JobMatchingConfigurationComparison(
    bool IsAvailable,
    bool? IsIdentical,
    IReadOnlyList<JobMatchingConfigurationDifference> Differences);

public sealed record JobMatchingConfigurationDifference(
    string Field,
    string? ValueA,
    string? ValueB);
