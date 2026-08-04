using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Requisitions;

public sealed record ChangeJobRequisitionStatusRequest(
    JobRequisitionStatus Status,
    DateOnly? ClosedAt);
