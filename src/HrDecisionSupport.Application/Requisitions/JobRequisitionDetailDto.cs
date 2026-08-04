using HrDecisionSupport.Application.Requisitions.Requirements;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Requisitions;

public sealed record JobRequisitionDetailDto(
    Guid Id,
    string RequisitionCode,
    string Title,
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    Guid PositionId,
    string PositionCode,
    string PositionName,
    string? Description,
    int OpeningsCount,
    JobRequisitionStatus JobRequisitionStatus,
    DateOnly OpenedAt,
    DateOnly? ClosedAt,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int RequirementCount,
    IReadOnlyList<JobRequisitionRequirementDto> Requirements);
