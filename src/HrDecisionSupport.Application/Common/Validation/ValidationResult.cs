namespace HrDecisionSupport.Application.Common.Validation;

public sealed class ValidationResult
{
    private readonly IReadOnlyList<ValidationError> _errors;

    private ValidationResult(IEnumerable<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var materializedErrors = errors.ToArray();
        if (materializedErrors.Any(static error => error is null))
        {
            throw new ArgumentException(
                "The validation error collection cannot contain null elements.",
                nameof(errors));
        }

        _errors = Array.AsReadOnly(materializedErrors);
    }

    public bool IsValid => _errors.Count == 0;
    public IReadOnlyList<ValidationError> Errors => _errors;

    public static ValidationResult Valid() => new([]);

    public static ValidationResult Invalid(IEnumerable<ValidationError> errors)
    {
        var result = new ValidationResult(errors);
        return result._errors.Count == 0
            ? throw new ArgumentException(
                "An invalid validation result must contain at least one error.",
                nameof(errors))
            : result;
    }

    public static ValidationResult Invalid(params ValidationError[] errors) =>
        Invalid((IEnumerable<ValidationError>)errors);

    public Result ToResult() =>
        IsValid ? Result.Success() : Result.ValidationFailure(_errors);
}
