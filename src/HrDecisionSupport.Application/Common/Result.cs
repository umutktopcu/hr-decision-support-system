namespace HrDecisionSupport.Application.Common;

public class Result
{
    private readonly IReadOnlyList<Error> _errors;

    protected Result(bool isSuccess, IEnumerable<Error> errors)
    {
        _errors = CreateReadOnlyErrors(errors);

        if (isSuccess && _errors.Count != 0)
        {
            throw new ArgumentException("A successful result cannot contain errors.", nameof(errors));
        }

        if (!isSuccess && _errors.Count == 0)
        {
            throw new ArgumentException("A failed result must contain at least one error.", nameof(errors));
        }

        IsSuccess = isSuccess;
    }

    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public IReadOnlyList<Error> Errors => _errors;
    public Error? Error => _errors.FirstOrDefault();

    public static Result Success() => new(true, []);

    public static Result Failure(string code, string message) =>
        Failure(global::HrDecisionSupport.Application.Common.Error.Failure(code, message));

    /// <summary>
    /// Creates a failed result while preserving the supplied error's type.
    /// </summary>
    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result(false, [error]);
    }

    public static Result ValidationFailure(IEnumerable<ValidationError> errors) =>
        new(false, GetValidationErrors(errors));

    public static Result ValidationFailure(params ValidationError[] errors) =>
        ValidationFailure((IEnumerable<ValidationError>)errors);

    public static Result NotFound(string code, string message) =>
        Failure(global::HrDecisionSupport.Application.Common.Error.NotFound(code, message));

    public static Result Conflict(string code, string message) =>
        Failure(global::HrDecisionSupport.Application.Common.Error.Conflict(code, message));

    protected static IReadOnlyList<Error> GetValidationErrors(
        IEnumerable<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var materializedErrors = errors.ToArray();
        if (materializedErrors.Any(static error => error is null))
        {
            throw new ArgumentException(
                "The validation error collection cannot contain null elements.",
                nameof(errors));
        }

        return Array.AsReadOnly(materializedErrors);
    }

    private static IReadOnlyList<Error> CreateReadOnlyErrors(IEnumerable<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);

        var materializedErrors = errors.ToArray();
        if (materializedErrors.Any(static error => error is null))
        {
            throw new ArgumentException(
                "The error collection cannot contain null elements.",
                nameof(errors));
        }

        return Array.AsReadOnly(materializedErrors);
    }
}

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value)
        : base(true, [])
    {
        _value = value;
    }

    private Result(IEnumerable<Error> errors)
        : base(false, errors)
    {
    }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result does not have a value.");

    public static Result<T> Success(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return new Result<T>(value);
    }

    public static new Result<T> Failure(string code, string message) =>
        Failure(global::HrDecisionSupport.Application.Common.Error.Failure(code, message));

    /// <summary>
    /// Creates a failed result while preserving the supplied error's type.
    /// </summary>
    public static new Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new Result<T>([error]);
    }

    public static new Result<T> ValidationFailure(IEnumerable<ValidationError> errors) =>
        new(GetValidationErrors(errors));

    public static new Result<T> ValidationFailure(params ValidationError[] errors) =>
        ValidationFailure((IEnumerable<ValidationError>)errors);

    public static new Result<T> NotFound(string code, string message) =>
        Failure(global::HrDecisionSupport.Application.Common.Error.NotFound(code, message));

    public static new Result<T> Conflict(string code, string message) =>
        Failure(global::HrDecisionSupport.Application.Common.Error.Conflict(code, message));
}
