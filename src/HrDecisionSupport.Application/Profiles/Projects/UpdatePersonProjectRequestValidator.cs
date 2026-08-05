using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.Projects;

public sealed class UpdatePersonProjectRequestValidator : IValidator<UpdatePersonProjectRequest>
{
    public ValidationResult Validate(UpdatePersonProjectRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        CreatePersonProjectRequestValidator.ValidateDetails(errors, instance.Role, instance.StartDate,
            instance.EndDate, instance.Description);
        return RequestValidation.ToResult(errors);
    }
}
