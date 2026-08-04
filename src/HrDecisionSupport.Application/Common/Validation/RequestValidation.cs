using System.Net.Mail;

namespace HrDecisionSupport.Application.Common.Validation;

internal static class RequestValidation
{
    internal static void RequiredString(
        ICollection<ValidationError> errors,
        string? value,
        int maxLength,
        string propertyName,
        string codePrefix)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError(
                $"{codePrefix}_required",
                $"{propertyName} is required.",
                propertyName));
            return;
        }

        MaxLength(errors, value.Trim(), maxLength, propertyName, codePrefix);
    }

    internal static void OptionalString(
        ICollection<ValidationError> errors,
        string? value,
        int maxLength,
        string propertyName,
        string codePrefix)
    {
        if (value is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(new ValidationError(
                $"{codePrefix}_whitespace",
                $"{propertyName} cannot contain only whitespace.",
                propertyName));
            return;
        }

        MaxLength(errors, value.Trim(), maxLength, propertyName, codePrefix);
    }

    internal static void OptionalEmail(
        ICollection<ValidationError> errors,
        string? value,
        string codePrefix)
    {
        const string propertyName = "Email";
        OptionalString(errors, value, 320, propertyName, codePrefix);

        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var normalized = value.Trim();
        try
        {
            var address = new MailAddress(normalized);
            if (!string.Equals(address.Address, normalized, StringComparison.OrdinalIgnoreCase)
                || !normalized.Contains('@', StringComparison.Ordinal))
            {
                AddInvalidEmail(errors, codePrefix);
            }
        }
        catch (FormatException)
        {
            AddInvalidEmail(errors, codePrefix);
        }
    }

    internal static ValidationResult ToResult(IReadOnlyCollection<ValidationError> errors) =>
        errors.Count == 0
            ? ValidationResult.Valid()
            : ValidationResult.Invalid(errors);

    private static void MaxLength(
        ICollection<ValidationError> errors,
        string value,
        int maxLength,
        string propertyName,
        string codePrefix)
    {
        if (value.Length > maxLength)
        {
            errors.Add(new ValidationError(
                $"{codePrefix}_max_length",
                $"{propertyName} cannot exceed {maxLength} characters.",
                propertyName));
        }
    }

    private static void AddInvalidEmail(
        ICollection<ValidationError> errors,
        string codePrefix) =>
        errors.Add(new ValidationError(
            $"{codePrefix}_invalid",
            "Email must be a valid email address.",
            "Email"));
}
