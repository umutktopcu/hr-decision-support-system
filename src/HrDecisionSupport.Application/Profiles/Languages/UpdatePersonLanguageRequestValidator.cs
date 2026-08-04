using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.Languages;

public sealed class UpdatePersonLanguageRequestValidator : IValidator<UpdatePersonLanguageRequest>
{
    public ValidationResult Validate(UpdatePersonLanguageRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        CreatePersonLanguageRequestValidator.ValidateLevel(errors, instance.ProficiencyLevel);
        return RequestValidation.ToResult(errors);
    }
}
