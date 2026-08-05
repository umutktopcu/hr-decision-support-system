using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.EmploymentHistory;

public sealed class CreateEmploymentHistoryRequestValidator : IValidator<CreateEmploymentHistoryRequest>
{
    public ValidationResult Validate(CreateEmploymentHistoryRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        if (instance.PersonId == Guid.Empty)
            errors.Add(new("person_id_required", "PersonId must not be empty.", nameof(instance.PersonId)));
        ValidateDetails(errors, instance.EmployerName, instance.PositionTitle, instance.StartDate,
            instance.EndDate, instance.Description);
        return RequestValidation.ToResult(errors);
    }

    internal static void ValidateDetails(ICollection<ValidationError> errors, string? employerName,
        string? positionTitle, DateOnly startDate, DateOnly? endDate, string? description)
    {
        RequestValidation.RequiredString(errors, employerName, 200, "EmployerName", "employer_name");
        RequestValidation.RequiredString(errors, positionTitle, 200, "PositionTitle", "position_title");
        RequestValidation.OptionalString(errors, description, 2000, "Description", "description");
        if (endDate < startDate)
            errors.Add(new("end_date_before_start_date", "EndDate cannot be before StartDate.", "EndDate"));
    }
}
