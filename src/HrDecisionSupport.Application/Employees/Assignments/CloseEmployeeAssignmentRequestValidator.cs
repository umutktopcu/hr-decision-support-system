using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Employees.Assignments;

public sealed class CloseEmployeeAssignmentRequestValidator
    : IValidator<CloseEmployeeAssignmentRequest>
{
    public ValidationResult Validate(CloseEmployeeAssignmentRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        EmployeeAssignmentValidation.RequiredDate(
            errors, instance.EndDate, nameof(instance.EndDate), "end_date_required");
        return RequestValidation.ToResult(errors);
    }
}
