namespace HrDecisionSupport.Application.CandidateEvaluations;

public sealed record UpdateCandidateEvaluationCaseRequest(
    string? ExternalReference,
    DateTime ReceivedAtUtc,
    string? Notes);
