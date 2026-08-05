using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.Certificates;

public sealed class UpdatePersonCertificateRequestValidator : IValidator<UpdatePersonCertificateRequest>
{
    public ValidationResult Validate(UpdatePersonCertificateRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        CreatePersonCertificateRequestValidator.ValidateMetadata(errors, instance.IssueDate,
            instance.ExpirationDate, instance.CredentialCode);
        return RequestValidation.ToResult(errors);
    }
}
