using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HrDecisionSupport.Tests;

public class CandidateImportDomainFoundationTests
{
    private DbContextOptions<HrDecisionSupportDbContext> CreateOptions()
    {
        return new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    [Fact]
    public async Task Candidate_Schema_SupportsNewFields()
    {
        var options = CreateOptions();
        await using var context = new HrDecisionSupportDbContext(options);

        var person = new Person { Id = Guid.NewGuid(), AnonymousCode = "CAND01", CreatedAtUtc = DateTime.UtcNow };
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            CandidateCode = "CAND01",
            CandidateSource = Domain.Enums.CandidateSource.Excel,
            ProfessionalTitle = "Senior Dev", // Nullable but supported
            AvailabilityDays = 15 // Nullable but supported
        };

        context.People.Add(person);
        context.Candidates.Add(candidate);
        await context.SaveChangesAsync();

        var saved = await context.Candidates.FindAsync(candidate.Id);
        Assert.NotNull(saved);
        Assert.Equal("Senior Dev", saved.ProfessionalTitle);
        Assert.Equal(15, saved.AvailabilityDays);
    }

    [Fact]
    public async Task CandidateWorkModePreference_Configuration_IsApplied()
    {
        var options = CreateOptions();
        await using var context = new HrDecisionSupportDbContext(options);

        var person = new Person { Id = Guid.NewGuid(), AnonymousCode = "CAND02", CreatedAtUtc = DateTime.UtcNow };
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            CandidateCode = "CAND02",
            CandidateSource = Domain.Enums.CandidateSource.Excel
        };
        var workMode = new WorkMode { Id = Guid.NewGuid(), Code = "REMOTE", Name = "Uzaktan" };
        
        var preference = new CandidateWorkModePreference
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            WorkModeId = workMode.Id
        };

        context.People.Add(person);
        context.Candidates.Add(candidate);
        context.WorkModes.Add(workMode);
        context.CandidateWorkModePreferences.Add(preference);
        await context.SaveChangesAsync();

        var saved = await context.CandidateWorkModePreferences
            .Include(p => p.Candidate)
            .Include(p => p.WorkMode)
            .SingleOrDefaultAsync(p => p.Id == preference.Id);
            
        Assert.NotNull(saved);
        Assert.Equal(candidate.Id, saved.CandidateId);
        Assert.Equal(workMode.Id, saved.WorkModeId);
    }

    [Fact]
    public async Task CandidateCareerFeatureSnapshot_Configuration_IsApplied()
    {
        var options = CreateOptions();
        await using var context = new HrDecisionSupportDbContext(options);

        var person = new Person { Id = Guid.NewGuid(), AnonymousCode = "CAND03", CreatedAtUtc = DateTime.UtcNow };
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            CandidateCode = "CAND03",
            CandidateSource = Domain.Enums.CandidateSource.Excel
        };

        var snapshot = new CandidateCareerFeatureSnapshot
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            TotalExperienceMonths = 96,
            BackendExperienceMonths = 48,
            PreviousCompanyAverageStayMonths = 24.5m,
            ShortestPreviousJobMonths = 12,
            LongestPreviousJobMonths = 36,
            LastPreviousCompanyStayMonths = 36,
            CompanyChangeCount = 3,
            JobChangeRate = 0.375m,
            FeatureSchemaVersion = "1.0",
            CalculatedAtUtc = DateTime.UtcNow
        };

        context.People.Add(person);
        context.Candidates.Add(candidate);
        context.CandidateCareerFeatureSnapshots.Add(snapshot);
        await context.SaveChangesAsync();

        var saved = await context.CandidateCareerFeatureSnapshots.FindAsync(snapshot.Id);
            
        Assert.NotNull(saved);
        Assert.Equal(96, saved.TotalExperienceMonths);
        Assert.Equal(48, saved.BackendExperienceMonths);
        Assert.Equal(24.5m, saved.PreviousCompanyAverageStayMonths);
        Assert.Equal(0.375m, saved.JobChangeRate);
        Assert.Equal("1.0", saved.FeatureSchemaVersion);
    }
}
