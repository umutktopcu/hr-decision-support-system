namespace HrDecisionSupport.Application.Requisitions;

public sealed record UpdateJobRequisitionRequest(
    string RequisitionCode,
    string Title,
    Guid DepartmentId,
    Guid PositionId,
    string? Description,
    int OpeningsCount,
    int? MinimumRelevantExperienceMonths = null,
    DateOnly OpenedAt = default,
    decimal? MandatorySkillCoverageThreshold = null);
