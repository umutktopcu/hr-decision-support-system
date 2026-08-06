using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Requisitions.Requirements;

public sealed record JobRequisitionRequirementDto(
    Guid Id,
    Guid JobRequisitionId,
    Guid CompetencyId,
    string CompetencyCode,
    string CompetencyName,
    CompetencyCategory CompetencyCategory,
    int? MinimumExperienceMonths,
    CompetencyProficiencyLevel? MinimumProficiencyLevel,
    bool IsRequired,
    string? Notes);
