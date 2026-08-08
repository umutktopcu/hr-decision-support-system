using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using System.Linq;

namespace HrDecisionSupport.Application.Requisitions;

public sealed class UpdateJobRequisitionRequestValidator
    : IValidator<UpdateJobRequisitionRequest>
{
    public ValidationResult Validate(UpdateJobRequisitionRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        JobRequisitionValidation.ValidateCommon(
            errors,
            instance.RequisitionCode,
            instance.Title,
            instance.DepartmentId,
            instance.PositionId,
            instance.Description,
            instance.OpeningsCount,
            instance.MinimumRelevantExperienceMonths,
            instance.OpenedAt,
            instance.MandatorySkillCoverageThreshold,
            instance.MinimumEducationLevel);

        if (instance.Requirements != null && !instance.Requirements.Any(r => r.IsRequired))
        {
            errors.Add(new("mandatory_skill_required", "At least one mandatory skill is required.", "Requirements"));
        }

        JobRequisitionValidation.ValidateLanguageRequirements(errors, instance.LanguageRequirements);

        return RequestValidation.ToResult(errors);
    }
}
