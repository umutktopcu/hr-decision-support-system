using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Requisitions;
using HrDecisionSupport.Application.Requisitions.Requirements;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace HrDecisionSupport.Tests.Web;

[Collection(PostgreSqlIntegrationCollection.Name)]
public class JobRequisitionsControllerIntegrationTests
{
    private readonly PostgreSqlIntegrationTestFixture _fixture;

    public JobRequisitionsControllerIntegrationTests(PostgreSqlIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgreSqlIntegrationFact]
    public async Task JobRequisition_RoundTrip_CreatesAndUpdatesProperly()
    {
        await _fixture.ResetDatabaseAsync();
        await using var context = _fixture.CreateDbContext();

        // Setup test data
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var csharp = TestDatabase.Competency("C#");
        var dotnetCore = TestDatabase.Competency(".NET Core");
        var aspnetCore = TestDatabase.Competency("ASP.NET Core");

        var workMode = new Domain.Entities.WorkMode { Id = Guid.NewGuid(), Name = "Hybrid" };
        var language = new Domain.Entities.Language { Id = Guid.NewGuid(), Name = "İngilizce", Code = "ENG" };

        context.AddRange(department, position, csharp, dotnetCore, aspnetCore, workMode, language);
        await context.SaveChangesAsync();

        var service = new JobRequisitionService(
            context,
            new CreateJobRequisitionRequestValidator(),
            new UpdateJobRequisitionRequestValidator(),
            new ChangeJobRequisitionStatusRequestValidator(),
            TimeProvider.System);

        var controller = new JobRequisitionsController(service);

        // 1. POST
        var createRequest = new CreateJobRequisitionRequest(
            "BD-API-TEST",
            "Backend Developer API Test",
            department.Id,
            position.Id,
            "API creation flow test.",
            1,
            24,
            new DateOnly(2026, 1, 1),
            null,
            0.50m,
            DegreeLevel.Bachelor,
            workMode.Id,
            true,
            new List<JobRequirementModel>
            {
                new(csharp.Id, null, null, true, null),
                new(dotnetCore.Id, null, null, true, null),
                new(aspnetCore.Id, null, null, false, null)
            },
            new List<JobLanguageRequirementModel>
            {
                new(language.Id, LanguageProficiencyLevel.B2, true)
            }
        );

        var createResponse = await controller.Create(createRequest, default) as CreatedAtActionResult;
        Assert.NotNull(createResponse);
        Assert.Equal(201, createResponse.StatusCode);
        Assert.Equal("Get", createResponse.ActionName);
        Assert.True(createResponse.RouteValues.ContainsKey("id"));

        var createdDto = createResponse.Value as JobRequisitionDto;
        Assert.NotNull(createdDto);
        var id = createdDto.Id;

        // 2. GET by ID
        var getResponse = await controller.Get(id, default) as OkObjectResult;
        Assert.NotNull(getResponse);
        var detailDto = getResponse.Value as JobRequisitionDetailDto;
        Assert.NotNull(detailDto);
        Assert.Equal("Backend Developer API Test", detailDto.Title);
        Assert.Equal(24, detailDto.MinimumRelevantExperienceMonths);
        Assert.Equal(DegreeLevel.Bachelor, detailDto.MinimumEducationLevel);
        Assert.Equal(workMode.Id, detailDto.WorkModeId);
        Assert.True(detailDto.WorkModeHardFilterEnabled);
        Assert.Equal(0.50m, detailDto.MandatorySkillCoverageThreshold);
        Assert.Equal(3, detailDto.Requirements.Count);
        Assert.Single(detailDto.LanguageRequirements);

        // 3. PUT Update
        var updateRequest = new UpdateJobRequisitionRequest(
            "BD-API-TEST-UPDATED",
            "Backend Developer API Test Updated",
            department.Id,
            position.Id,
            "Updated desc.",
            2,
            36,
            new DateOnly(2026, 1, 1),
            0.60m,
            DegreeLevel.Master,
            workMode.Id,
            false,
            new List<JobRequirementModel>
            {
                new(csharp.Id, null, null, true, null)
            },
            new List<JobLanguageRequirementModel>()
        );

        var putResponse = await controller.Update(id, updateRequest, default) as OkObjectResult;
        Assert.NotNull(putResponse);

        // 4. GET by ID to verify update
        var getResponse2 = await controller.Get(id, default) as OkObjectResult;
        var detailDto2 = getResponse2?.Value as JobRequisitionDetailDto;
        Assert.NotNull(detailDto2);
        Assert.Equal("Backend Developer API Test Updated", detailDto2.Title);
        Assert.Equal(36, detailDto2.MinimumRelevantExperienceMonths);
        Assert.Equal(DegreeLevel.Master, detailDto2.MinimumEducationLevel);
        Assert.False(detailDto2.WorkModeHardFilterEnabled);
        Assert.Equal(0.60m, detailDto2.MandatorySkillCoverageThreshold);
        Assert.Single(detailDto2.Requirements); // Only C# now
        Assert.Empty(detailDto2.LanguageRequirements);

        // 5. PATCH status
        var statusRequest = new ChangeJobRequisitionStatusRequest(JobRequisitionStatus.Open, null);
        var patchResponse = await controller.ChangeStatus(id, statusRequest, default) as OkObjectResult;
        Assert.NotNull(patchResponse);
        var patchDto = patchResponse.Value as JobRequisitionDto;
        Assert.NotNull(patchDto);
        Assert.Equal(JobRequisitionStatus.Open, patchDto.JobRequisitionStatus);

        // Explicit rollback transaction is handled by PostgreSqlIntegrationFact
    }

    [PostgreSqlIntegrationFact]
    public async Task Options_ReturnsExpectedLookupGroups()
    {
        await _fixture.ResetDatabaseAsync();
        await using var context = _fixture.CreateDbContext();

        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var csharp = TestDatabase.Competency("C#");
        var workMode = new Domain.Entities.WorkMode { Id = Guid.NewGuid(), Name = "Remote" };
        var language = new Domain.Entities.Language { Id = Guid.NewGuid(), Name = "Spanish", Code = "ESP" };

        context.AddRange(department, position, csharp, workMode, language);
        await context.SaveChangesAsync();

        var controller = new JobRequisitionOptionsController(context);

        var response = await controller.GetOptions(default) as OkObjectResult;
        Assert.NotNull(response);

        var json = System.Text.Json.JsonSerializer.Serialize(response.Value);
        var doc = System.Text.Json.JsonDocument.Parse(json);

        Assert.True(doc.RootElement.GetProperty("Departments").GetArrayLength() > 0);
        Assert.True(doc.RootElement.GetProperty("Positions").GetArrayLength() > 0);
        Assert.True(doc.RootElement.GetProperty("Competencies").GetArrayLength() > 0);
        Assert.True(doc.RootElement.GetProperty("WorkModes").GetArrayLength() > 0);
        Assert.True(doc.RootElement.GetProperty("Languages").GetArrayLength() > 0);
    }
}
