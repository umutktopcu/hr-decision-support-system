using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.Sectors;

public sealed class UpdatePersonSectorExperienceRequestValidator : IValidator<UpdatePersonSectorExperienceRequest>
{
    public ValidationResult Validate(UpdatePersonSectorExperienceRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        CreatePersonSectorExperienceRequestValidator.ValidateDetails(errors, instance.ExperienceMonths, instance.Notes);
        return RequestValidation.ToResult(errors);
    }
}
