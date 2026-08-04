namespace HrDecisionSupport.Application.Profiles.Projects;

public sealed record CreatePersonProjectRequest(
    Guid PersonId,
    Guid ProjectId,
    string? Role,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Description);
