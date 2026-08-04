using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.Sectors;

public sealed class CreatePersonSectorExperienceRequestValidator : IValidator<CreatePersonSectorExperienceRequest>
{
    public ValidationResult Validate(CreatePersonSectorExperienceRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        if (instance.PersonId == Guid.Empty)
            errors.Add(new("person_id_required", "PersonId must not be empty.", nameof(instance.PersonId)));
        if (instance.SectorId == Guid.Empty)
            errors.Add(new("sector_id_required", "SectorId must not be empty.", nameof(instance.SectorId)));
        ValidateDetails(errors, instance.ExperienceMonths, instance.Notes);
        return RequestValidation.ToResult(errors);
    }

    internal static void ValidateDetails(ICollection<ValidationError> errors, int? experienceMonths,
        string? notes)
    {
        if (experienceMonths < 0)
            errors.Add(new("experience_months_negative", "ExperienceMonths cannot be negative.", "ExperienceMonths"));
        RequestValidation.OptionalString(errors, notes, 1000, "Notes", "notes");
    }
}
