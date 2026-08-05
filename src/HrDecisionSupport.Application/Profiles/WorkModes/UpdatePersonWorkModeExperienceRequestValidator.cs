using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.WorkModes;

public sealed class UpdatePersonWorkModeExperienceRequestValidator : IValidator<UpdatePersonWorkModeExperienceRequest>
{
    public ValidationResult Validate(UpdatePersonWorkModeExperienceRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        CreatePersonWorkModeExperienceRequestValidator.ValidateExperienceMonths(errors, instance.ExperienceMonths);
        return RequestValidation.ToResult(errors);
    }
}
