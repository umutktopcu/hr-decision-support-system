namespace HrDecisionSupport.Application.Profiles.EmploymentHistory;

public sealed record EmploymentHistoryDto(
    Guid Id,
    Guid PersonId,
    string EmployerName,
    string PositionTitle,
    DateOnly StartDate,
    DateOnly? EndDate,
    string? Description);
