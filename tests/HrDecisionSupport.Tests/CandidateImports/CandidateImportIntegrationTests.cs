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
        services.AddInfrastructure(options => options.UseNpgsql(_fixture.ConnectionString));
        
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
