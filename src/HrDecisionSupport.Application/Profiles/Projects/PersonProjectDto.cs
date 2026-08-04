namespace HrDecisionSupport.Application.Profiles.Projects;

public sealed record PersonProjectDto(
    Guid Id,
    Guid PersonId,
    Guid ProjectId,
    string ProjectName,
    string? Role,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Description);
