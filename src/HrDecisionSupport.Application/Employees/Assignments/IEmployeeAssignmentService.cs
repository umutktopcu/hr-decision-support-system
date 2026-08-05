using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.Employees.Assignments;

public interface IEmployeeAssignmentService
{
    Task<Result<IReadOnlyList<EmployeeAssignmentDto>>> ListByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAssignmentDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAssignmentDto>> CreateAsync(
        CreateEmployeeAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAssignmentDto>> UpdateAsync(
        Guid id,
        UpdateEmployeeAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAssignmentDto>> ChangeCurrentAsync(
        ChangeCurrentEmployeeAssignmentRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeAssignmentDto>> CloseAsync(
        Guid id,
        CloseEmployeeAssignmentRequest request,
        CancellationToken cancellationToken = default);
}
