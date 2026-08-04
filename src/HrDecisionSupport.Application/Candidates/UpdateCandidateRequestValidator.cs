using HrDecisionSupport.Application.Candidates.Dtos;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Candidates;

public sealed class UpdateCandidateRequestValidator : IValidator<UpdateCandidateRequest>
{
    public ValidationResult Validate(UpdateCandidateRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();

        RequestValidation.RequiredString(
            errors, instance.CandidateCode, 50, nameof(instance.CandidateCode), "candidate_code");
        CreateCandidateRequestValidator.ValidateCommon(
            errors,
            instance.FirstName,
            instance.LastName,
            instance.Email,
            instance.PhoneNumber,
            instance.CandidateSource,
            instance.ExternalCandidateId);

        return RequestValidation.ToResult(errors);
    }
}
