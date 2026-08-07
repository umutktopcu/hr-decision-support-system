using HrDecisionSupport.Domain.Entities;

namespace HrDecisionSupport.Application.PreScreening.Models;

public sealed record CandidatePreScreeningResult(
    Guid CandidateId,
    Guid JobRequisitionId,
    SkillEvaluationResult MandatorySkillResult,
    SkillEvaluationResult OverallSkillResult,
    ExperienceEvaluationResult ExperienceResult,
    bool EligibleForSemanticEvaluation,
    IReadOnlyList<PreScreeningFailureReason> FailureReasons,
    decimal MandatorySkillThresholdUsed,
    decimal OverallSkillThresholdUsed);

public sealed record SkillEvaluationResult(
    int TotalRequired,
    int TotalMatched,
    decimal Coverage,
    EvaluationStatus Status,
    bool Passed,
    IReadOnlyList<Guid> MatchedCompetencyIds,
    IReadOnlyList<Guid> MissingCompetencyIds);

public sealed record ExperienceEvaluationResult(
    int? CandidateRelevantExperienceMonths,
    int? RequiredRelevantExperienceMonths,
    EvaluationStatus Status,
    bool Passed);

public enum EvaluationStatus
{
    NotApplicable,
    Pass,
    Fail
}

public enum PreScreeningFailureReason
{
    MandatorySkillCoverageBelowThreshold,
    OverallSkillCoverageBelowThreshold,
    RelevantExperienceBelowMinimum,
    MissingRelevantExperienceData,
    UnsupportedRelevantExperienceSource
}
