using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

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
            instance.OverallSkillCoverageThreshold);

        return RequestValidation.ToResult(errors);
    }
}
