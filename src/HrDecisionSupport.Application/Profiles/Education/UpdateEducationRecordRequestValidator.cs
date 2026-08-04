using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.Education;

public sealed class UpdateEducationRecordRequestValidator : IValidator<UpdateEducationRecordRequest>
{
    public ValidationResult Validate(UpdateEducationRecordRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        CreateEducationRecordRequestValidator.ValidateFields(errors, instance.Institution,
            instance.FieldOfStudy, instance.DegreeLevel, instance.StartDate, instance.GraduationDate);
        return RequestValidation.ToResult(errors);
    }
}
