namespace HrDecisionSupport.Application.Requisitions;

public sealed record UpdateJobRequisitionRequest(
    string RequisitionCode,
    string Title,
    Guid DepartmentId,
    Guid PositionId,
    string? Description,
    int OpeningsCount,
    int? MinimumRelevantExperienceMonths,
    DateOnly OpenedAt,
    decimal? MandatorySkillCoverageThreshold = null,
    decimal? OverallSkillCoverageThreshold = null);
