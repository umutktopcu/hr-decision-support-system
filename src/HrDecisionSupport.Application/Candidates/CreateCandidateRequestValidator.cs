using HrDecisionSupport.Application.Candidates.Dtos;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Candidates;

public sealed class CreateCandidateRequestValidator : IValidator<CreateCandidateRequest>
{
    public ValidationResult Validate(CreateCandidateRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();

        RequestValidation.RequiredString(
            errors, instance.CandidateCode, 50, nameof(instance.CandidateCode), "candidate_code");
        RequestValidation.RequiredString(
            errors, instance.AnonymousCode, 100, nameof(instance.AnonymousCode), "anonymous_code");
        ValidateCommon(
            errors,
            instance.FirstName,
            instance.LastName,
            instance.Email,
            instance.PhoneNumber,
            instance.CandidateSource,
            instance.ExternalCandidateId);

        return RequestValidation.ToResult(errors);
    }

    internal static void ValidateCommon(
        ICollection<ValidationError> errors,
        string? firstName,
        string? lastName,
        string? email,
        string? phoneNumber,
        CandidateSource candidateSource,
        string? externalCandidateId)
    {
        RequestValidation.OptionalString(errors, firstName, 100, "FirstName", "first_name");
        RequestValidation.OptionalString(errors, lastName, 100, "LastName", "last_name");
        RequestValidation.OptionalEmail(errors, email, "email");
        RequestValidation.OptionalString(errors, phoneNumber, 30, "PhoneNumber", "phone_number");
        RequestValidation.OptionalString(
            errors,
            externalCandidateId,
            200,
            "ExternalCandidateId",
            "external_candidate_id");

        if (!Enum.IsDefined(candidateSource))
        {
            errors.Add(new ValidationError(
                "candidate_source_invalid",
                "CandidateSource must be a defined value.",
                "CandidateSource"));
        }
    }
}
