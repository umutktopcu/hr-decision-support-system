namespace HrDecisionSupport.Application.Profiles.WorkModes;

public sealed record CreatePersonWorkModeExperienceRequest(
    Guid PersonId,
    Guid WorkModeId,
    int? ExperienceMonths);
