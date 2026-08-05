using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Competencies;

public sealed record CreatePersonCompetencyRequest(
    Guid PersonId,
    Guid CompetencyId,
    int? ExperienceMonths,
    ProficiencyLevel? ProficiencyLevel);
