using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Employees.Assignments;

public sealed class ChangeCurrentEmployeeAssignmentRequestValidator
    : IValidator<ChangeCurrentEmployeeAssignmentRequest>
{
    public ValidationResult Validate(ChangeCurrentEmployeeAssignmentRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        EmployeeAssignmentValidation.RequiredGuid(
            errors, instance.EmployeeId, nameof(instance.EmployeeId), "employee_id_required");
        EmployeeAssignmentValidation.RequiredGuid(
            errors, instance.DepartmentId, nameof(instance.DepartmentId), "department_id_required");
        EmployeeAssignmentValidation.RequiredGuid(
            errors, instance.PositionId, nameof(instance.PositionId), "position_id_required");
        EmployeeAssignmentValidation.RequiredDate(
            errors, instance.NewStartDate, nameof(instance.NewStartDate), "new_start_date_required");
        return RequestValidation.ToResult(errors);
    }
}
