using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.Certificates;

public sealed class CreatePersonCertificateRequestValidator : IValidator<CreatePersonCertificateRequest>
{
    public ValidationResult Validate(CreatePersonCertificateRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        if (instance.PersonId == Guid.Empty)
            errors.Add(new("person_id_required", "PersonId must not be empty.", nameof(instance.PersonId)));
        if (instance.CertificateId == Guid.Empty)
            errors.Add(new("certificate_id_required", "CertificateId must not be empty.", nameof(instance.CertificateId)));
        ValidateMetadata(errors, instance.IssueDate, instance.ExpirationDate, instance.CredentialCode);
        return RequestValidation.ToResult(errors);
    }

    internal static void ValidateMetadata(ICollection<ValidationError> errors, DateOnly? issueDate,
        DateOnly? expirationDate, string? credentialCode)
    {
        RequestValidation.OptionalString(errors, credentialCode, 200, "CredentialCode", "credential_code");
        if (expirationDate < issueDate)
            errors.Add(new("expiration_date_before_issue_date", "ExpirationDate cannot be before IssueDate.", "ExpirationDate"));
    }
}
