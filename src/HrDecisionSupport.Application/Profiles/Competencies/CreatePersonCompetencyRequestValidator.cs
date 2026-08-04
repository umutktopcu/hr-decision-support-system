using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Competencies;

public sealed class CreatePersonCompetencyRequestValidator : IValidator<CreatePersonCompetencyRequest>
{
    public ValidationResult Validate(CreatePersonCompetencyRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();

        if (instance.PersonId == Guid.Empty)
            errors.Add(new("person_id_required", "PersonId must not be empty.", nameof(instance.PersonId)));
        if (instance.CompetencyId == Guid.Empty)
            errors.Add(new("competency_id_required", "CompetencyId must not be empty.", nameof(instance.CompetencyId)));

        ValidateMetadata(errors, instance.ExperienceMonths, instance.ProficiencyLevel);
        return RequestValidation.ToResult(errors);
    }

    internal static void ValidateMetadata(
        ICollection<ValidationError> errors,
        int? experienceMonths,
        ProficiencyLevel? proficiencyLevel)
    {
        if (experienceMonths < 0)
            errors.Add(new("experience_months_negative", "ExperienceMonths cannot be negative.", "ExperienceMonths"));
        if (proficiencyLevel.HasValue && !Enum.IsDefined(proficiencyLevel.Value))
            errors.Add(new("proficiency_level_invalid", "ProficiencyLevel must be a defined value.", "ProficiencyLevel"));
    }
}
