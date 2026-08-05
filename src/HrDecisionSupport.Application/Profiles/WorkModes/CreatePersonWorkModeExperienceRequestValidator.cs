using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.WorkModes;

public sealed class CreatePersonWorkModeExperienceRequestValidator : IValidator<CreatePersonWorkModeExperienceRequest>
{
    public ValidationResult Validate(CreatePersonWorkModeExperienceRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        if (instance.PersonId == Guid.Empty)
            errors.Add(new("person_id_required", "PersonId must not be empty.", nameof(instance.PersonId)));
        if (instance.WorkModeId == Guid.Empty)
            errors.Add(new("work_mode_id_required", "WorkModeId must not be empty.", nameof(instance.WorkModeId)));
        ValidateExperienceMonths(errors, instance.ExperienceMonths);
        return RequestValidation.ToResult(errors);
    }

    internal static void ValidateExperienceMonths(ICollection<ValidationError> errors, int? experienceMonths)
    {
        if (experienceMonths < 0)
            errors.Add(new("experience_months_negative", "ExperienceMonths cannot be negative.", "ExperienceMonths"));
    }
}
