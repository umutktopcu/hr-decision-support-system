using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Competencies;

public sealed record PersonCompetencyDto(
    Guid Id,
    Guid PersonId,
    Guid CompetencyId,
    string CompetencyCode,
    string CompetencyName,
    CompetencyCategory CompetencyCategory,
    int? ExperienceMonths,
    CompetencyProficiencyLevel? ProficiencyLevel);
