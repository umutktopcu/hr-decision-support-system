namespace HrDecisionSupport.Application.Employees.Assignments;

public sealed record UpdateEmployeeAssignmentRequest(
    Guid DepartmentId,
    Guid PositionId,
    DateOnly StartDate,
    DateOnly? EndDate);
