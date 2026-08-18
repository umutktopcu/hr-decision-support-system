using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application;
using HrDecisionSupport.Application.CandidateImports;
using HrDecisionSupport.Infrastructure;
using HrDecisionSupport.Application.CandidateImports.Models;
using HrDecisionSupport.Application.CandidateImports.Spreadsheet;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Pgvector.EntityFrameworkCore;
using Xunit;

namespace HrDecisionSupport.Tests.CandidateImports;

[Collection(nameof(PostgreSqlIntegrationCollection))]
public sealed class CandidateImportIntegrationTests
{
    private readonly PostgreSqlIntegrationTestFixture _fixture;

    public CandidateImportIntegrationTests(PostgreSqlIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_InsertsNewCandidates_AndSkipsExisting()
    {
        await _fixture.ResetDatabaseAsync();

        // Arrange
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(options => options.UseNpgsql(
            _fixture.ConnectionString,
            npgsqlOptions => npgsqlOptions.UseVector()));

        var mockReader = new MockCandidateImportSourceReader();
        services.RemoveAll<ICandidateImportSourceReader>();
        services.AddSingleton<ICandidateImportSourceReader>(mockReader);

        await using var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetRequiredService<HrDecisionSupport.Application.Common.Interfaces.IHrDecisionSupportDbContext>();

        // Ensure Requisition exists
        var requisitionId = Guid.Parse("0eeb3ac1-6e82-4251-ba70-2e0ebf46ed87");
        await dbContext.JobRequisitions.AddAsync(new JobRequisition
        {
            Id = requisitionId,
            RequisitionCode = "TEST-REQ",
            Title = "Backend Dev",
            Description = "Test",
            OpeningsCount = 1,
            JobRequisitionStatus = JobRequisitionStatus.Open,
            DepartmentId = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = serviceProvider.GetRequiredService<CandidateImportService>();

        // Act 1: First Import
        using var dummyStream = new MemoryStream();
        var result1 = await service.ImportAsync(dummyStream);

        // Assert 1
        Assert.Equal(2, result1.Inserted); // Two valid candidates
        Assert.Equal(0, result1.SkippedExisting);
        Assert.Equal(0, result1.Failed);

        var dbContextForAssert = _fixture.CreateDbContext();
        var candidates = await dbContextForAssert.Candidates.ToListAsync();
        Assert.Equal(2, candidates.Count);

        // Act 2: Second Import (Duplicate run)
        var result2 = await service.ImportAsync(dummyStream);

        // Assert 2
        Assert.Equal(0, result2.Inserted);
        Assert.Equal(2, result2.SkippedExisting);
        Assert.Equal(0, result2.Failed);
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_WithExistingSharedCatalogs_ReusesExistingEntities()
    {
        await _fixture.ResetDatabaseAsync();

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(options => options.UseNpgsql(
            _fixture.ConnectionString,
            npgsqlOptions => npgsqlOptions.UseVector()));

        var mockReader = new MockCandidateImportSourceReader();
        services.RemoveAll<ICandidateImportSourceReader>();
        services.AddSingleton<ICandidateImportSourceReader>(mockReader);

        await using var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetRequiredService<HrDecisionSupport.Application.Common.Interfaces.IHrDecisionSupportDbContext>();

        // 1. Seed existing shared catalogs (like Employee import would have done)
        var existingCompetency = new Competency { Id = Guid.NewGuid(), Code = "C#", Name = "C#", CompetencyCategory = CompetencyCategory.ProgrammingLanguage };
        var existingLang = new Language { Id = Guid.NewGuid(), Code = "LANG_" + HrDecisionSupport.Application.Common.ProfileMappings.SharedProfileConstants.Hash("İngilizce"), Name = "İngilizce" };
        var existingCert = new Certificate { Id = Guid.NewGuid(), Code = "CERT_" + HrDecisionSupport.Application.Common.ProfileMappings.SharedProfileConstants.Hash("AWS"), Name = "AWS" };
        var existingSector = new Sector { Id = Guid.NewGuid(), Code = "Finans", Name = "Finans" };
        var existingWm = new WorkMode { Id = Guid.NewGuid(), Code = "REMOTE", Name = "Remote" };
        var existingProject = new Project { Id = Guid.NewGuid(), Name = "Proj 1" };

        dbContext.Competencies.Add(existingCompetency);
        dbContext.Languages.Add(existingLang);
        dbContext.Certificates.Add(existingCert);
        dbContext.Sectors.Add(existingSector);
        dbContext.WorkModes.Add(existingWm);
        dbContext.Projects.Add(existingProject);

        var requisitionId = Guid.Parse("0eeb3ac1-6e82-4251-ba70-2e0ebf46ed87");
        dbContext.JobRequisitions.Add(new JobRequisition
        {
            Id = requisitionId,
            RequisitionCode = "TEST-REQ",
            Title = "Backend Dev",
            Description = "Test",
            OpeningsCount = 1,
            JobRequisitionStatus = JobRequisitionStatus.Open,
            DepartmentId = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = serviceProvider.GetRequiredService<CandidateImportService>();

        // Act
        using var dummyStream = new MemoryStream();
        var result = await service.ImportAsync(dummyStream);

        // Assert
        Assert.Equal(0, result.Failed);
        Assert.Equal(2, result.Inserted);

        var dbContextForAssert = _fixture.CreateDbContext();

        // Existing catalogs should NOT be duplicated
        Assert.Equal(1, await dbContextForAssert.Competencies.CountAsync(c => c.Code == "C#"));
        Assert.Equal(1, await dbContextForAssert.Languages.CountAsync(c => c.Code == existingLang.Code));
        Assert.Equal(1, await dbContextForAssert.Certificates.CountAsync(c => c.Code == existingCert.Code));
        Assert.Equal(1, await dbContextForAssert.Sectors.CountAsync(c => c.Code == "Finans"));
        Assert.Equal(1, await dbContextForAssert.WorkModes.CountAsync(c => c.Code == "REMOTE"));
        Assert.Equal(1, await dbContextForAssert.Projects.CountAsync(c => c.Name == "Proj 1"));

        // New catalogs should be added once per batch
        Assert.Equal(1, await dbContextForAssert.Competencies.CountAsync(c => c.Code == "Java"));
        Assert.Equal(1, await dbContextForAssert.WorkModes.CountAsync(c => c.Code == "HYBRID"));
        Assert.Equal(1, await dbContextForAssert.Projects.CountAsync(c => c.Name == "Proj 2"));
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_WithDuplicateProjectsInDatabase_LinksToDeterministicProject()
    {
        await _fixture.ResetDatabaseAsync();

        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(options => options.UseNpgsql(
            _fixture.ConnectionString,
            npgsqlOptions => npgsqlOptions.UseVector()));

        var mockReader = new MockCandidateImportSourceReader();
        services.RemoveAll<ICandidateImportSourceReader>();
        services.AddSingleton<ICandidateImportSourceReader>(mockReader);

        await using var serviceProvider = services.BuildServiceProvider();
        var dbContext = serviceProvider.GetRequiredService<HrDecisionSupport.Application.Common.Interfaces.IHrDecisionSupportDbContext>();

        // 1. Seed two duplicate projects (same name, different IDs)
        var p1 = new Project { Id = Guid.Parse("11111111-1111-1111-1111-111111111111"), Name = "Proj 1" };
        var p2 = new Project { Id = Guid.Parse("22222222-2222-2222-2222-222222222222"), Name = "Proj 1" };

        dbContext.Projects.Add(p1);
        dbContext.Projects.Add(p2);

        var requisitionId = Guid.Parse("0eeb3ac1-6e82-4251-ba70-2e0ebf46ed87");
        dbContext.JobRequisitions.Add(new JobRequisition
        {
            Id = requisitionId,
            RequisitionCode = "TEST-REQ",
            Title = "Backend Dev",
            Description = "Test",
            OpeningsCount = 1,
            JobRequisitionStatus = JobRequisitionStatus.Open,
            DepartmentId = Guid.NewGuid(),
            PositionId = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow
        });
        await dbContext.SaveChangesAsync();

        var service = serviceProvider.GetRequiredService<CandidateImportService>();

        // Act
        using var dummyStream = new MemoryStream();
        var result = await service.ImportAsync(dummyStream);

        // Assert
        Assert.Equal(0, result.Failed);

        var dbContextForAssert = _fixture.CreateDbContext();

        // Ensure the candidate was linked to p1 (the one with the smaller Guid deterministically returned by OrderBy(Id).First())
        var personProject = await dbContextForAssert.PersonProjects
            .Include(pp => pp.Person)
            .Include(pp => pp.Project)
            .FirstOrDefaultAsync(pp => pp.Person.AnonymousCode == "A100" && pp.Project.Name == "Proj 1");

        Assert.NotNull(personProject);
        Assert.Equal(p1.Id, personProject.ProjectId);
    }

    private class MockCandidateImportSourceReader : ICandidateImportSourceReader
    {
        public Task<IReadOnlyList<CandidateInputRecord>> ReadRecordsAsync(Stream sourceStream, CancellationToken cancellationToken = default)
        {
            var records = new List<CandidateInputRecord>
            {
                new CandidateInputRecord(1, "A100", "Backend", "Backend Dev", 5, 3, "Backend", "Tech A", "01.2020-01.2023", "C#", "ASP.NET", "Proj 1", "Lisans", "Bilgisayar Müh.", "AWS", "İngilizce (C1)", "Finans", "Uzaktan", 14),
                new CandidateInputRecord(2, "A101", "Backend", "Senior Backend Dev", 8, 8, "Backend", "Tech B", "01.2018-01.2023", "Java", "Spring", "Proj 2", "Lisans", "Yazılım Müh.", null, "İngilizce", "E-Ticaret", "Hibrit", 30)
            };
            return Task.FromResult<IReadOnlyList<CandidateInputRecord>>(records);
        }
    }
}
