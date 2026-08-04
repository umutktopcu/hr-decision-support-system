namespace HrDecisionSupport.Application.CandidateEvaluations;

public sealed record CreateCandidateEvaluationCaseRequest(
    Guid CandidateId,
    Guid JobRequisitionId,
    string? ExternalReference,
    DateTime ReceivedAtUtc,
    string? Notes);
