using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.CandidateEvaluations;

public sealed record CandidateEvaluationCaseDto(
    Guid Id,
    Guid CandidateId,
    string CandidateCode,
    string AnonymousCode,
    string? CandidateFirstName,
    string? CandidateLastName,
    Guid JobRequisitionId,
    string RequisitionCode,
    string RequisitionTitle,
    Guid DepartmentId,
    string DepartmentName,
    Guid PositionId,
    string PositionName,
    string? ExternalReference,
    DateTime ReceivedAtUtc,
    CandidateEvaluationStatus Status,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
