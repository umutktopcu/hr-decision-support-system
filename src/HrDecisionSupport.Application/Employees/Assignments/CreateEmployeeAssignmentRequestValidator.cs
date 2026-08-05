using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Employees.Assignments;

public sealed class CreateEmployeeAssignmentRequestValidator
    : IValidator<CreateEmployeeAssignmentRequest>
{
    public ValidationResult Validate(CreateEmployeeAssignmentRequest instance)
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
            errors, instance.StartDate, nameof(instance.StartDate), "start_date_required");
        EmployeeAssignmentValidation.DateRange(
            errors, instance.StartDate, instance.EndDate);
        return RequestValidation.ToResult(errors);
    }
}

internal static class EmployeeAssignmentValidation
{
    internal static void RequiredGuid(
        ICollection<ValidationError> errors,
        Guid value,
        string propertyName,
        string code)
    {
        if (value == Guid.Empty)
            errors.Add(new(code, $"{propertyName} must not be empty.", propertyName));
    }

    internal static void RequiredDate(
        ICollection<ValidationError> errors,
        DateOnly value,
        string propertyName,
        string code)
    {
        if (value == default)
            errors.Add(new(code, $"{propertyName} must not be the default date.", propertyName));
    }

    internal static void DateRange(
        ICollection<ValidationError> errors,
        DateOnly startDate,
        DateOnly? endDate)
    {
        if (endDate < startDate)
        {
            errors.Add(new(
                "end_date_before_start_date",
                "EndDate cannot be before StartDate.",
                "EndDate"));
        }
    }
}
