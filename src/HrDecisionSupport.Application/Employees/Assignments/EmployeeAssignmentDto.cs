namespace HrDecisionSupport.Application.Employees.Assignments;

public sealed record EmployeeAssignmentDto(
    Guid Id,
    Guid EmployeeId,
    string EmployeeCode,
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    Guid PositionId,
    string PositionCode,
    string PositionName,
    DateOnly StartDate,
    DateOnly? EndDate,
    bool IsCurrent);
