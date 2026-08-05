using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Requisitions.Requirements;

public sealed record CreateJobRequisitionRequirementRequest(
    Guid JobRequisitionId,
    Guid CompetencyId,
    int? MinimumExperienceMonths,
    ProficiencyLevel? MinimumProficiencyLevel,
    bool IsRequired,
    string? Notes);
