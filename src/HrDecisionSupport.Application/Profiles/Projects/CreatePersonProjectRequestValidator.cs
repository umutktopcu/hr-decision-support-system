using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.Projects;

public sealed class CreatePersonProjectRequestValidator : IValidator<CreatePersonProjectRequest>
{
    public ValidationResult Validate(CreatePersonProjectRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        if (instance.PersonId == Guid.Empty)
            errors.Add(new("person_id_required", "PersonId must not be empty.", nameof(instance.PersonId)));
        if (instance.ProjectId == Guid.Empty)
            errors.Add(new("project_id_required", "ProjectId must not be empty.", nameof(instance.ProjectId)));
        ValidateDetails(errors, instance.Role, instance.StartDate, instance.EndDate, instance.Description);
        return RequestValidation.ToResult(errors);
    }

    internal static void ValidateDetails(ICollection<ValidationError> errors, string? role,
        DateOnly? startDate, DateOnly? endDate, string? description)
    {
        RequestValidation.OptionalString(errors, role, 200, "Role", "role");
        RequestValidation.OptionalString(errors, description, 2000, "Description", "description");
        if (endDate < startDate)
            errors.Add(new("end_date_before_start_date", "EndDate cannot be before StartDate.", "EndDate"));
    }
}
