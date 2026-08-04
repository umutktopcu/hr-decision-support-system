namespace HrDecisionSupport.Application.Profiles.Projects;

public sealed record UpdatePersonProjectRequest(
    string? Role,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Description);
