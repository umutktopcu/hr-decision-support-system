namespace HrDecisionSupport.Application.Profiles.EmploymentHistory;

public sealed record CreateEmploymentHistoryRequest(
    Guid PersonId,
    string EmployerName,
    string PositionTitle,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Description);
