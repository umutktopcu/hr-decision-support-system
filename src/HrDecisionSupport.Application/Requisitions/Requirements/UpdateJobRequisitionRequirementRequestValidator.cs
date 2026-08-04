using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Requisitions.Requirements;

public sealed class UpdateJobRequisitionRequirementRequestValidator
    : IValidator<UpdateJobRequisitionRequirementRequest>
{
    public ValidationResult Validate(UpdateJobRequisitionRequirementRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        JobRequisitionRequirementValidation.ValidateMetadata(
            errors,
            instance.MinimumExperienceMonths,
            instance.MinimumProficiencyLevel,
            instance.Notes);
        return RequestValidation.ToResult(errors);
    }
}
