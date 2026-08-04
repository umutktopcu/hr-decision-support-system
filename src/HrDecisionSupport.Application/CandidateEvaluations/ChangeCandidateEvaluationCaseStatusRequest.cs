using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.CandidateEvaluations;

public sealed record ChangeCandidateEvaluationCaseStatusRequest(
    CandidateEvaluationStatus Status);
