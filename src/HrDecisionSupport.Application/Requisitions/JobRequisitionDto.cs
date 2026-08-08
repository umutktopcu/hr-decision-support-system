using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Requisitions;

public sealed record JobRequisitionDto(
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
    int? MinimumRelevantExperienceMonths,
    JobRequisitionStatus JobRequisitionStatus,
    DateOnly OpenedAt,
    DateOnly? ClosedAt,
    decimal? MandatorySkillCoverageThreshold,
    decimal? OverallSkillCoverageThreshold,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int RequirementCount);
