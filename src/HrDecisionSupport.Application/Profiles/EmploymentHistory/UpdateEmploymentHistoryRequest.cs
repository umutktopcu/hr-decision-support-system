namespace HrDecisionSupport.Application.Profiles.EmploymentHistory;

public sealed record UpdateEmploymentHistoryRequest(
    string EmployerName,
    string PositionTitle,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Description);
