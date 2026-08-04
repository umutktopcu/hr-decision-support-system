using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees.Dtos;

namespace HrDecisionSupport.Application.Employees;

public sealed class UpdateEmployeeRequestValidator : IValidator<UpdateEmployeeRequest>
{
    public ValidationResult Validate(UpdateEmployeeRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();

        RequestValidation.RequiredString(
            errors, instance.EmployeeCode, 50, nameof(instance.EmployeeCode), "employee_code");
        CreateEmployeeRequestValidator.ValidatePersonFields(
            errors, instance.FirstName, instance.LastName, instance.Email, instance.PhoneNumber);
        CreateEmployeeRequestValidator.ValidateEmployment(
            errors, instance.HireDate, instance.TerminationDate, instance.EmploymentStatus);

        return RequestValidation.ToResult(errors);
    }
}
