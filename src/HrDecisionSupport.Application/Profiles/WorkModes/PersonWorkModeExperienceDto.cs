namespace HrDecisionSupport.Application.Profiles.WorkModes;

public sealed record PersonWorkModeExperienceDto(
    Guid Id,
    Guid PersonId,
    Guid WorkModeId,
    string WorkModeCode,
    string WorkModeName,
    int? ExperienceMonths);
