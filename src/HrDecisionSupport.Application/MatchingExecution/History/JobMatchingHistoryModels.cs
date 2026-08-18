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
