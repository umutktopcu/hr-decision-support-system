using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Languages;

public sealed class CreatePersonLanguageRequestValidator : IValidator<CreatePersonLanguageRequest>
{
    public ValidationResult Validate(CreatePersonLanguageRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        if (instance.PersonId == Guid.Empty)
            errors.Add(new("person_id_required", "PersonId must not be empty.", nameof(instance.PersonId)));
        if (instance.LanguageId == Guid.Empty)
            errors.Add(new("language_id_required", "LanguageId must not be empty.", nameof(instance.LanguageId)));
        ValidateLevel(errors, instance.ProficiencyLevel);
        return RequestValidation.ToResult(errors);
    }

    internal static void ValidateLevel(ICollection<ValidationError> errors, LanguageProficiencyLevel level)
    {
        if (!Enum.IsDefined(level))
            errors.Add(new("proficiency_level_invalid", "ProficiencyLevel must be a defined value.", "ProficiencyLevel"));
    }
}
