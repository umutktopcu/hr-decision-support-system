using HrDecisionSupport.Domain.Entities;

namespace HrDecisionSupport.Application.Common;

internal sealed record PersonData(
    string? FirstName,
    string? LastName,
    string? Email,
    string? PhoneNumber)
{
    internal static PersonData Create(
        string? firstName,
        string? lastName,
        string? email,
        string? phoneNumber) =>
        new(
            Normalize(firstName),
            Normalize(lastName),
            Normalize(email),
            Normalize(phoneNumber));

    internal Person CreatePerson(string anonymousCode, DateTime utcNow) =>
        new()
        {
            Id = Guid.NewGuid(),
            AnonymousCode = anonymousCode,
            FirstName = FirstName,
            LastName = LastName,
            Email = Email,
            PhoneNumber = PhoneNumber,
            CreatedAtUtc = utcNow
        };

    internal bool ConflictsWith(Person person) =>
        Conflicts(person.FirstName, FirstName, StringComparison.Ordinal)
        || Conflicts(person.LastName, LastName, StringComparison.Ordinal)
        || Conflicts(person.Email, Email, StringComparison.OrdinalIgnoreCase)
        || Conflicts(person.PhoneNumber, PhoneNumber, StringComparison.Ordinal);

    internal bool CompleteMissingFields(Person person)
    {
        var changed = false;
        if (person.FirstName is null && FirstName is not null)
        {
            person.FirstName = FirstName;
            changed = true;
        }

        if (person.LastName is null && LastName is not null)
        {
            person.LastName = LastName;
            changed = true;
        }

        if (person.Email is null && Email is not null)
        {
            person.Email = Email;
            changed = true;
        }

        if (person.PhoneNumber is null && PhoneNumber is not null)
        {
            person.PhoneNumber = PhoneNumber;
            changed = true;
        }

        return changed;
    }

    internal void ApplyTo(Person person)
    {
        person.FirstName = FirstName;
        person.LastName = LastName;
        person.Email = Email;
        person.PhoneNumber = PhoneNumber;
    }

    private static string? Normalize(string? value) => value?.Trim();

    private static bool Conflicts(
        string? existing,
        string? supplied,
        StringComparison comparison) =>
        existing is not null
        && supplied is not null
        && !string.Equals(existing, supplied, comparison);

}
