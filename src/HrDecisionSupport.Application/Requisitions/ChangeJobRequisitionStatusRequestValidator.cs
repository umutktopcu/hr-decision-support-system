using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Requisitions;

public sealed class ChangeJobRequisitionStatusRequestValidator
    : IValidator<ChangeJobRequisitionStatusRequest>
{
    public ValidationResult Validate(ChangeJobRequisitionStatusRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();

        if (!Enum.IsDefined(instance.Status))
        {
            errors.Add(new(
                "job_requisition_status_invalid",
                "Status must be a defined value.",
                nameof(instance.Status)));
        }
        else if (instance.Status is JobRequisitionStatus.Closed or JobRequisitionStatus.Cancelled)
        {
            if (!instance.ClosedAt.HasValue)
            {
                errors.Add(new(
                    "closed_at_required",
                    "ClosedAt is required for a closed or cancelled requisition.",
                    nameof(instance.ClosedAt)));
            }
        }
        else if (instance.ClosedAt.HasValue)
        {
            errors.Add(new(
                "closed_at_not_allowed",
                "ClosedAt is only allowed for a closed or cancelled requisition.",
                nameof(instance.ClosedAt)));
        }

        return RequestValidation.ToResult(errors);
    }
}
