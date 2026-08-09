using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

internal static class TestDatabase
{
    public static readonly Guid DefaultWorkModeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    public static readonly Guid DefaultCompetencyId = Guid.Parse("20000000-0000-0000-0000-000000000002");

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

    internal static Competency Competency(string name = "C#", bool isActive = true) =>
        new()
        {
            Id = Guid.NewGuid(), Code = $"CMP-{Guid.NewGuid():N}"[..20], Name = name,
            CompetencyCategory = CompetencyCategory.Skill, IsActive = isActive
        };

    internal static JobRequisition JobRequisition(
        Department department,
        Position position,
        string? code = null,
        JobRequisitionStatus status = JobRequisitionStatus.Draft,
        DateOnly? openedAt = null,
        DateOnly? closedAt = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            RequisitionCode = code ?? $"REQ-{Guid.NewGuid():N}"[..20],
            Title = "Software Engineer",
            DepartmentId = department.Id,
            PositionId = position.Id,
            Description = "Build products",
            OpeningsCount = 2,
            JobRequisitionStatus = status,
            OpenedAt = openedAt ?? new DateOnly(2026, 1, 1),
            ClosedAt = closedAt,
            CreatedAtUtc = DateTime.UtcNow,
            WorkModeId = DefaultWorkModeId
        };

    internal static JobRequisitionRequirement JobRequisitionRequirement(
        JobRequisition requisition,
        Competency competency,
        bool isRequired = true) =>
        new()
        {
            Id = Guid.NewGuid(),
            JobRequisitionId = requisition.Id,
            CompetencyId = competency.Id,
            MinimumExperienceMonths = 12,
            MinimumProficiencyLevel = CompetencyProficiencyLevel.Intermediate,
            IsRequired = isRequired,
            Notes = "Relevant experience"
        };

    internal static Candidate Candidate(Person person, string? code = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            CandidateCode = code ?? $"CAN-{Guid.NewGuid():N}"[..20],
            CandidateSource = CandidateSource.Other
        };

    internal static CandidateEvaluationCase CandidateEvaluationCase(
        Candidate candidate,
        JobRequisition requisition,
        CandidateEvaluationStatus status = CandidateEvaluationStatus.New,
        DateTime? receivedAtUtc = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            JobRequisitionId = requisition.Id,
            ExternalReference = "EXT-1",
            ReceivedAtUtc = receivedAtUtc ?? DateTime.UtcNow,
            Status = status,
            Notes = "Evaluation notes",
            CreatedAtUtc = DateTime.UtcNow
        };

    internal static Certificate Certificate(string name = "Cloud Certificate", string? issuer = "Issuer") =>
        new()
        {
            Id = Guid.NewGuid(), Code = $"CRT-{Guid.NewGuid():N}"[..20], Name = name, Issuer = issuer
        };

    internal static Language Language(string name = "English") =>
        new()
        {
            Id = Guid.NewGuid(), Code = $"LNG-{Guid.NewGuid():N}"[..20], Name = name
        };

    internal static Project Project(string name = "Project") =>
        new() { Id = Guid.NewGuid(), Name = name, Description = "Catalog project" };

    internal static Sector Sector(string name = "Technology") =>
        new() { Id = Guid.NewGuid(), Code = $"SEC-{Guid.NewGuid():N}"[..20], Name = name };

    internal static WorkMode WorkMode(string name = "Remote") =>
        new() { Id = Guid.NewGuid(), Code = $"WM-{Guid.NewGuid():N}"[..20], Name = name };
}
