using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Profiles.EmploymentHistory;

public sealed class UpdateEmploymentHistoryRequestValidator : IValidator<UpdateEmploymentHistoryRequest>
{
    public ValidationResult Validate(UpdateEmploymentHistoryRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        CreateEmploymentHistoryRequestValidator.ValidateDetails(errors, instance.EmployerName,
            instance.PositionTitle, instance.StartDate, instance.EndDate, instance.Description);
        return RequestValidation.ToResult(errors);
    }
}
