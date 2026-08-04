using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.CandidateEvaluations;

public sealed class ChangeCandidateEvaluationCaseStatusRequestValidator
    : IValidator<ChangeCandidateEvaluationCaseStatusRequest>
{
    public ValidationResult Validate(ChangeCandidateEvaluationCaseStatusRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        if (!Enum.IsDefined(instance.Status))
        {
            errors.Add(new(
                "candidate_evaluation_case_status_invalid",
                "Status must be a defined value.",
                nameof(instance.Status)));
        }

        return RequestValidation.ToResult(errors);
    }
}
