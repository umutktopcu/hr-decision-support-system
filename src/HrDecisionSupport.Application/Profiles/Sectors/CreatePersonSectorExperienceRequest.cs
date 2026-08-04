namespace HrDecisionSupport.Application.Profiles.Sectors;

public sealed record CreatePersonSectorExperienceRequest(
    Guid PersonId,
    Guid SectorId,
    int? ExperienceMonths,
    string? Notes);
