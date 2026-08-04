namespace HrDecisionSupport.Application.Common;

public record Error
{
    public Error(string code, string message, ErrorType type = ErrorType.Failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(
                nameof(type),
                type,
                "The error type must be a defined value.");
        }

        Code = code;
        Message = message;
        Type = type;
    }

    public string Code { get; }
    public string Message { get; }
    public ErrorType Type { get; }

    public static Error NotFound(string code, string message) =>
        new(code, message, ErrorType.NotFound);

    public static Error Conflict(string code, string message) =>
        new(code, message, ErrorType.Conflict);

    public static Error Failure(string code, string message) =>
        new(code, message, ErrorType.Failure);
}
