using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees.Dtos;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Employees;

public sealed class CreateEmployeeRequestValidator : IValidator<CreateEmployeeRequest>
{
    public ValidationResult Validate(CreateEmployeeRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();

        RequestValidation.RequiredString(
            errors, instance.EmployeeCode, 50, nameof(instance.EmployeeCode), "employee_code");
        RequestValidation.RequiredString(
            errors, instance.AnonymousCode, 100, nameof(instance.AnonymousCode), "anonymous_code");
        ValidatePersonFields(errors, instance.FirstName, instance.LastName, instance.Email, instance.PhoneNumber);
        ValidateEmployment(
            errors,
            instance.HireDate,
            instance.TerminationDate,
            instance.EmploymentStatus);

        if (instance.InitialDepartmentId == Guid.Empty)
        {
            errors.Add(new ValidationError(
                "initial_department_id_required",
                "InitialDepartmentId must not be empty.",
                nameof(instance.InitialDepartmentId)));
        }

        if (instance.InitialPositionId == Guid.Empty)
        {
            errors.Add(new ValidationError(
                "initial_position_id_required",
                "InitialPositionId must not be empty.",
                nameof(instance.InitialPositionId)));
        }

        if (instance.InitialAssignmentStartDate < instance.HireDate)
        {
            errors.Add(new ValidationError(
                "initial_assignment_start_date_before_hire_date",
                "InitialAssignmentStartDate cannot be before HireDate.",
                nameof(instance.InitialAssignmentStartDate)));
        }

        return RequestValidation.ToResult(errors);
    }

    internal static void ValidatePersonFields(
        ICollection<ValidationError> errors,
        string? firstName,
        string? lastName,
        string? email,
        string? phoneNumber)
    {
        RequestValidation.OptionalString(errors, firstName, 100, "FirstName", "first_name");
        RequestValidation.OptionalString(errors, lastName, 100, "LastName", "last_name");
        RequestValidation.OptionalEmail(errors, email, "email");
        RequestValidation.OptionalString(errors, phoneNumber, 30, "PhoneNumber", "phone_number");
    }

    internal static void ValidateEmployment(
        ICollection<ValidationError> errors,
        DateOnly hireDate,
        DateOnly? terminationDate,
        EmploymentStatus employmentStatus)
    {
        if (hireDate == default)
        {
            errors.Add(new ValidationError(
                "hire_date_required",
                "HireDate must be a valid date.",
                "HireDate"));
        }

        if (!Enum.IsDefined(employmentStatus))
        {
            errors.Add(new ValidationError(
                "employment_status_invalid",
                "EmploymentStatus must be a defined value.",
                "EmploymentStatus"));
        }

        if (terminationDate < hireDate)
        {
            errors.Add(new ValidationError(
                "termination_date_before_hire_date",
                "TerminationDate cannot be before HireDate.",
                "TerminationDate"));
        }

        if (employmentStatus == EmploymentStatus.Terminated && terminationDate is null)
        {
            errors.Add(new ValidationError(
                "termination_date_required",
                "TerminationDate is required for a terminated employee.",
                "TerminationDate"));
        }
        else if (employmentStatus != EmploymentStatus.Terminated && terminationDate is not null)
        {
            errors.Add(new ValidationError(
                "termination_date_not_allowed",
                "TerminationDate must be null unless the employee is terminated.",
                "TerminationDate"));
        }
    }
}
