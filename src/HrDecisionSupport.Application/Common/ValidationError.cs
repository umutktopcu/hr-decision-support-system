namespace HrDecisionSupport.Application.Common;

public sealed record ValidationError : Error
{
    public ValidationError(string code, string message, string propertyName)
        : base(code, message, ErrorType.Validation)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        PropertyName = propertyName;
    }

    public string PropertyName { get; }
}
