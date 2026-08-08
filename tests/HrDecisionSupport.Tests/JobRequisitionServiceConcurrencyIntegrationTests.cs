using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Requisitions;
using HrDecisionSupport.Application.Requisitions.Requirements;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Tests.Requisitions;

[Collection(PostgreSqlIntegrationCollection.Name)]
public class JobRequisitionServiceConcurrencyIntegrationTests
{
    private readonly PostgreSqlIntegrationTestFixture _fixture;

    public JobRequisitionServiceConcurrencyIntegrationTests(PostgreSqlIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgreSqlIntegrationFact]
    public async Task UpdateAsync_ReplacesChildren_WithoutConcurrencyException()
    {
        // 1. Arrange: Setup DB and initial data
        await _fixture.ResetDatabaseAsync();
        await using var context = _fixture.CreateDbContext();

        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var competency = TestDatabase.Competency("Concurrency_Comp");
        var workMode = new Domain.Entities.WorkMode { Id = Guid.NewGuid(), Name = "Remote" };
        var language = new Domain.Entities.Language { Id = Guid.NewGuid(), Name = "French", Code = "FRA" };

        var requisition = TestDatabase.JobRequisition(department, position, "REQ-CONCURRENCY-1");

        // Add initial requirements
        var initialReq = new JobRequisitionRequirement
        {
            Id = Guid.NewGuid(),
            JobRequisitionId = requisition.Id,
            CompetencyId = competency.Id,
            MinimumExperienceMonths = 12,
            MinimumProficiencyLevel = CompetencyProficiencyLevel.Intermediate,
            IsRequired = true
        };
        var initialLang = new JobLanguageRequirement
        {
            Id = Guid.NewGuid(),
            JobRequisitionId = requisition.Id,
            LanguageId = language.Id,
            MinimumProficiency = LanguageProficiencyLevel.B1,
            HardFilterEnabled = true
        };

        requisition.Requirements.Add(initialReq);
        requisition.LanguageRequirements.Add(initialLang);

        context.AddRange(department, position, competency, workMode, language, requisition);
        await context.SaveChangesAsync();

        // Use a new context for the service to ensure no state leaks
        await using var serviceContext = _fixture.CreateDbContext();

        var service = new JobRequisitionService(
            serviceContext,
            new CreateJobRequisitionRequestValidator(),
            new UpdateJobRequisitionRequestValidator(),
            new ChangeJobRequisitionStatusRequestValidator(),
            TimeProvider.System);

        // 2. Act: Update with new children
        var updateRequest = new UpdateJobRequisitionRequest(
            "REQ-CONCURRENCY-1",
            "Updated Title",
            department.Id,
            position.Id,
            "Updated Description",
            2,
            24, // changed
            new DateOnly(2026, 1, 1),
            0.8m,
            DegreeLevel.Master,
            workMode.Id,
            true,
            new List<JobRequirementModel>
            {
                new(competency.Id, 24, CompetencyProficiencyLevel.Advanced, false, "Updated notes")
            },
            new List<JobLanguageRequirementModel>
            {
                new(language.Id, LanguageProficiencyLevel.C1, false)
            });

        var result = await service.UpdateAsync(requisition.Id, updateRequest);

        // 3. Assert: Success without exception, children correctly replaced
        Assert.True(result.IsSuccess, "Update should succeed without DbUpdateConcurrencyException.");

        // Verify via fresh DB context
        await using var assertContext = _fixture.CreateDbContext();
        var updatedReq = await assertContext.JobRequisitions
            .Include(r => r.Requirements)
            .Include(r => r.LanguageRequirements)
            .SingleAsync(r => r.Id == requisition.Id);

        // Verify requirements replaced
        Assert.Single(updatedReq.Requirements);
        var req = updatedReq.Requirements.First();
        Assert.NotEqual(initialReq.Id, req.Id);
        Assert.Equal(24, req.MinimumExperienceMonths);
        Assert.Equal(CompetencyProficiencyLevel.Advanced, req.MinimumProficiencyLevel);
        Assert.False(req.IsRequired);
        Assert.Equal("Updated notes", req.Notes);

        // Verify languages replaced
        Assert.Single(updatedReq.LanguageRequirements);
        var lang = updatedReq.LanguageRequirements.First();
        Assert.NotEqual(initialLang.Id, lang.Id);
        Assert.Equal(LanguageProficiencyLevel.C1, lang.MinimumProficiency);
        Assert.False(lang.HardFilterEnabled);
    }
}
