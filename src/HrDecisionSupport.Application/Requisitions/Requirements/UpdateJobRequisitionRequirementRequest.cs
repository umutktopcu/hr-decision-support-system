using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Requisitions.Requirements;

public sealed record UpdateJobRequisitionRequirementRequest(
    int? MinimumExperienceMonths,
    CompetencyProficiencyLevel? MinimumProficiencyLevel,
    bool IsRequired,
    string? Notes);
