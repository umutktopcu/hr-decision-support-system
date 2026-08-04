namespace HrDecisionSupport.Application.Common.Validation;

public interface IValidator<in T>
{
    ValidationResult Validate(T instance);
}
