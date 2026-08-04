using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Employees.Assignments;

public sealed class UpdateEmployeeAssignmentRequestValidator
    : IValidator<UpdateEmployeeAssignmentRequest>
{
    public ValidationResult Validate(UpdateEmployeeAssignmentRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        EmployeeAssignmentValidation.RequiredGuid(
            errors, instance.DepartmentId, nameof(instance.DepartmentId), "department_id_required");
        EmployeeAssignmentValidation.RequiredGuid(
            errors, instance.PositionId, nameof(instance.PositionId), "position_id_required");
        EmployeeAssignmentValidation.RequiredDate(
            errors, instance.StartDate, nameof(instance.StartDate), "start_date_required");
        EmployeeAssignmentValidation.DateRange(errors, instance.StartDate, instance.EndDate);
        return RequestValidation.ToResult(errors);
    }
}
