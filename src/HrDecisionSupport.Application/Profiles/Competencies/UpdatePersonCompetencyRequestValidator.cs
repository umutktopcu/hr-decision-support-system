using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.Competencies;

public sealed class UpdatePersonCompetencyRequestValidator : IValidator<UpdatePersonCompetencyRequest>
{
    public ValidationResult Validate(UpdatePersonCompetencyRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        CreatePersonCompetencyRequestValidator.ValidateMetadata(
            errors, instance.ExperienceMonths, instance.ProficiencyLevel);
        return RequestValidation.ToResult(errors);
    }
}
