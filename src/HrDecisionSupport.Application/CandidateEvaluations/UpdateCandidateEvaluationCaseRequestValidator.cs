using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.CandidateEvaluations;

public sealed class UpdateCandidateEvaluationCaseRequestValidator
    : IValidator<UpdateCandidateEvaluationCaseRequest>
{
    public ValidationResult Validate(UpdateCandidateEvaluationCaseRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        CandidateEvaluationCaseValidation.ValidateMutableFields(
            errors,
            instance.ExternalReference,
            instance.ReceivedAtUtc,
            instance.Notes);
        return RequestValidation.ToResult(errors);
    }
}
