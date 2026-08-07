namespace HrDecisionSupport.Application.Requisitions;

public sealed record CreateJobRequisitionRequest(
    string RequisitionCode,
    string Title,
    Guid DepartmentId,
    Guid PositionId,
    string? Description,
    int OpeningsCount,
    int? MinimumRelevantExperienceMonths,
    DateOnly OpenedAt,
    DateOnly? ClosedAt,
    decimal? MandatorySkillCoverageThreshold = null,
    decimal? OverallSkillCoverageThreshold = null);
