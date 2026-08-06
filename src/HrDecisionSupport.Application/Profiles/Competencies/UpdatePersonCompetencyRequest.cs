using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Competencies;

public sealed record UpdatePersonCompetencyRequest(
    int? ExperienceMonths,
    CompetencyProficiencyLevel? ProficiencyLevel);
