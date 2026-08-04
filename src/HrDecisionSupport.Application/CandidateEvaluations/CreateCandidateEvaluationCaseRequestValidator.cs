using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.CandidateEvaluations;

public sealed class CreateCandidateEvaluationCaseRequestValidator
    : IValidator<CreateCandidateEvaluationCaseRequest>
{
    public ValidationResult Validate(CreateCandidateEvaluationCaseRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();

        CandidateEvaluationCaseValidation.RequiredGuid(
            errors,
            instance.CandidateId,
            nameof(instance.CandidateId),
            "candidate_id_required");
        CandidateEvaluationCaseValidation.RequiredGuid(
            errors,
            instance.JobRequisitionId,
            nameof(instance.JobRequisitionId),
            "job_requisition_id_required");
        CandidateEvaluationCaseValidation.ValidateMutableFields(
            errors,
            instance.ExternalReference,
            instance.ReceivedAtUtc,
            instance.Notes);

        return RequestValidation.ToResult(errors);
    }
}

internal static class CandidateEvaluationCaseValidation
{
    internal static void ValidateMutableFields(
        ICollection<ValidationError> errors,
        string? externalReference,
        DateTime receivedAtUtc,
        string? notes)
    {
        RequestValidation.OptionalString(
            errors,
            externalReference,
            200,
            "ExternalReference",
            "external_reference");
        RequestValidation.OptionalString(errors, notes, 2000, "Notes", "notes");

        if (receivedAtUtc == default)
        {
            errors.Add(new(
                "received_at_utc_required",
                "ReceivedAtUtc must not be the default value.",
                "ReceivedAtUtc"));
        }
        else if (receivedAtUtc.Kind != DateTimeKind.Utc)
        {
            errors.Add(new(
                "received_at_utc_invalid",
                "ReceivedAtUtc must use UTC DateTimeKind.",
                "ReceivedAtUtc"));
        }
    }

    internal static void RequiredGuid(
        ICollection<ValidationError> errors,
        Guid value,
        string propertyName,
        string code)
    {
        if (value == Guid.Empty)
            errors.Add(new(code, $"{propertyName} must not be empty.", propertyName));
    }
}
