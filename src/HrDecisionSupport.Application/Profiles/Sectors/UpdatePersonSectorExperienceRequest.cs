namespace HrDecisionSupport.Application.Profiles.Sectors;

public sealed record UpdatePersonSectorExperienceRequest(
    int? ExperienceMonths,
    string? Notes);
