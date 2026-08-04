using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Education;

public sealed class CreateEducationRecordRequestValidator : IValidator<CreateEducationRecordRequest>
{
    public ValidationResult Validate(CreateEducationRecordRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        if (instance.PersonId == Guid.Empty)
            errors.Add(new("person_id_required", "PersonId must not be empty.", nameof(instance.PersonId)));
        ValidateFields(errors, instance.Institution, instance.FieldOfStudy, instance.DegreeLevel,
            instance.StartDate, instance.GraduationDate);
        return RequestValidation.ToResult(errors);
    }

    internal static void ValidateFields(ICollection<ValidationError> errors, string? institution,
        string? fieldOfStudy, DegreeLevel degreeLevel, DateOnly? startDate, DateOnly? graduationDate)
    {
        RequestValidation.RequiredString(errors, institution, 250, "Institution", "institution");
        RequestValidation.OptionalString(errors, fieldOfStudy, 200, "FieldOfStudy", "field_of_study");
        if (!Enum.IsDefined(degreeLevel))
            errors.Add(new("degree_level_invalid", "DegreeLevel must be a defined value.", "DegreeLevel"));
        if (graduationDate < startDate)
            errors.Add(new("graduation_date_before_start_date", "GraduationDate cannot be before StartDate.", "GraduationDate"));
    }
}
