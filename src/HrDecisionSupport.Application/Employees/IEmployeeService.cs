using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Employees.Dtos;

namespace HrDecisionSupport.Application.Employees;

public interface IEmployeeService
{
    Task<Result<IReadOnlyList<EmployeeListItemDto>>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailsDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailsDto>> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<EmployeeDetailsDto>> UpdateAsync(
        Guid id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default);
}
