using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.PreScreening.Models;

public sealed record CandidatePreScreeningResult(
    Guid CandidateId,
    Guid JobRequisitionId,
    SkillEvaluationResult MandatorySkillResult,
    SkillEvaluationResult PreferredSkillResult,
    ExperienceEvaluationResult ExperienceResult,
    EducationEvaluationResult EducationResult,
    WorkModeEvaluationResult WorkModeResult,
    IReadOnlyList<LanguageEvaluationResult> LanguageResults,
    bool EligibleForSemanticEvaluation,
    IReadOnlyList<PreScreeningFailureReason> FailureReasons,
    decimal MandatorySkillThresholdUsed);

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

public sealed record EducationEvaluationResult(
    DegreeLevel? RequiredLevel,
    DegreeLevel? CandidateHighestLevel,
    EvaluationStatus Status,
    bool Passed);

public sealed record WorkModeEvaluationResult(
    Guid? RequiredWorkModeId,
    bool HardFilterEnabled,
    bool CandidateSupportsWorkMode,
    EvaluationStatus Status,
    bool Passed);

public sealed record LanguageEvaluationResult(
    Guid LanguageId,
    LanguageProficiencyLevel RequiredProficiency,
    LanguageProficiencyLevel? CandidateProficiency,
    bool HardFilterEnabled,
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
    RelevantExperienceBelowMinimum,
    MissingRelevantExperienceData,
    UnsupportedRelevantExperienceSource,
    EducationLevelBelowMinimum,
    WorkModeMismatch,
    LanguageRequirementNotMet
}
