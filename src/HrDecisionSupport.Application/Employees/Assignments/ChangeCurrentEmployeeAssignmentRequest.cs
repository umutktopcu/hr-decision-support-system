namespace HrDecisionSupport.Application.Employees.Assignments;

public sealed record ChangeCurrentEmployeeAssignmentRequest(
    Guid EmployeeId,
    Guid DepartmentId,
    Guid PositionId,
    DateOnly NewStartDate);
