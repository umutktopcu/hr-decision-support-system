namespace HrDecisionSupport.Application.Profiles.Sectors;

public sealed record PersonSectorExperienceDto(
    Guid Id,
    Guid PersonId,
    Guid SectorId,
    string SectorCode,
    string SectorName,
    int? ExperienceMonths,
    string? Notes);
