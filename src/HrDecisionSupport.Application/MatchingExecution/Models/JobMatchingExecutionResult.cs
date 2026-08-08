using System;
using System.Collections.Generic;

namespace HrDecisionSupport.Application.MatchingExecution.Models;

public sealed record JobMatchingRequest(
    int RetrievalTopN = 100,
    int FinalTopN = 20);

public sealed record JobMatchingExecutionResult(
    Guid JobRequisitionId,
    JobMatchingStatistics Statistics,
    HardFilterBreakdown? HardFilterBreakdown,
    IReadOnlyList<JobMatchingCandidateResult> Candidates);

public sealed record JobMatchingStatistics(
    int CandidatePoolCount,
    int ExistingEvaluationCaseCount,
    int CreatedEvaluationCaseCount,
    int PreScreenedCandidateCount,
    int EligibleAfterHardFilters,
    int RejectedByHardFilters,
    int EmbeddingInputCount,
    int RequestedRetrievalTopN,
    int EmbeddingRetrievedCount,
    int CrossEncoderInputCount,
    int RequestedFinalTopN,
    int FinalReturnedCount);

public sealed record HardFilterBreakdown(
    GateStatistics MandatorySkill,
    GateStatistics Experience,
    GateStatistics Education,
    GateStatistics WorkMode,
    GateStatistics Language);

public sealed record GateStatistics(
    int PassCount,
    int FailCount,
    int NotApplicableCount);

public sealed record JobMatchingCandidateResult(
    Guid CandidateId,
    string? CandidateCode,
    string DisplayName,
    int SkillTier,
    decimal MandatorySkillCoverage,
    decimal PreferredSkillCoverage,
    double? EmbeddingScore,
    double? CrossEncoderRawScore,
    double JobFitScore);
