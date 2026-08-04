using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

internal static class TestDatabase
{
    internal static HrDecisionSupportDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseInMemoryDatabase($"hr-decision-support-{Guid.NewGuid():N}")
            .Options;
        return new HrDecisionSupportDbContext(options);
    }

    internal static Person Person(
        string anonymousCode,
        string? firstName = "Ada",
        string? lastName = "Lovelace",
        string? email = "ada@example.com",
        string? phoneNumber = "+90-555-000-0000") =>
        new()
        {
            Id = Guid.NewGuid(),
            AnonymousCode = anonymousCode,
            FirstName = firstName,
            LastName = lastName,
            Email = email,
            PhoneNumber = phoneNumber,
            CreatedAtUtc = DateTime.UtcNow
        };

    internal static Department Department(bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = $"DEP-{Guid.NewGuid():N}"[..20],
            Name = "Engineering",
            IsActive = isActive
        };

    internal static Position Position(bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            Code = $"POS-{Guid.NewGuid():N}"[..20],
            Name = "Software Engineer",
            IsActive = isActive
        };
}
