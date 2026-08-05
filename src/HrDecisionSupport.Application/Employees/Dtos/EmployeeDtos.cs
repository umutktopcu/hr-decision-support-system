using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Employees.Dtos;

public sealed record EmployeeListItemDto(
    Guid Id,
    string EmployeeCode,
    string AnonymousCode,
    string? FirstName,
    string? LastName,
    string? Email,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    EmploymentStatus EmploymentStatus,
    Guid? CurrentDepartmentId,
    string? CurrentDepartmentName,
    Guid? CurrentPositionId,
    string? CurrentPositionName);

public sealed record EmployeeDetailsDto(
    Guid Id,
    Guid PersonId,
    string EmployeeCode,
    string AnonymousCode,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    EmploymentStatus EmploymentStatus,
    IReadOnlyList<EmployeeAssignmentDto> Assignments);

public sealed record EmployeeAssignmentDto(
    Guid Id,
    Guid DepartmentId,
    string DepartmentName,
    Guid PositionId,
    string PositionName,
    DateOnly StartDate,
    DateOnly? EndDate);

public sealed record CreateEmployeeRequest(
    string EmployeeCode,
    string AnonymousCode,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    EmploymentStatus EmploymentStatus,
    Guid InitialDepartmentId,
    Guid InitialPositionId,
    DateOnly? InitialAssignmentStartDate);

public sealed record UpdateEmployeeRequest(
    string EmployeeCode,
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    EmploymentStatus EmploymentStatus);
