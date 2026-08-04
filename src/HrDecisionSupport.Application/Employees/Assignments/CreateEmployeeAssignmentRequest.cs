namespace HrDecisionSupport.Application.Employees.Assignments;

public sealed record CreateEmployeeAssignmentRequest(
    Guid EmployeeId,
    Guid DepartmentId,
    Guid PositionId,
    DateOnly StartDate,
    DateOnly? EndDate);
