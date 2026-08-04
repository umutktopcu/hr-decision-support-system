using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Requisitions.Requirements;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;

namespace HrDecisionSupport.Tests;

public class JobRequisitionRequirementServiceTests
{
    [Fact]
    public async Task List_ExistingRequisitionWithoutRequirements_ReturnsSuccessfulEmptyList()
    {
        await using var context = TestDatabase.CreateContext();
        var data = SeedData();
        context.AddRange(data.Department, data.Position, data.Requisition);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var result = await Service(context).ListByRequisitionAsync(data.Requisition.Id);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task List_MissingRequisition_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(
            await Service(context).ListByRequisitionAsync(Guid.NewGuid()),
            "job_requisition_not_found",
            ErrorType.NotFound);
    }

    [Fact]
    public async Task List_EmptyId_ReturnsValidationFailure()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(
            await Service(context).ListByRequisitionAsync(Guid.Empty),
            "job_requisition_id_required",
            ErrorType.Validation);
    }

    [Fact]
    public async Task List_ProjectsCompetencyAndOrdersRequiredThenNameThenId()
    {
        await using var context = TestDatabase.CreateContext();
        var data = SeedData();
        var alpha = TestDatabase.Competency("Alpha");
        var zulu = TestDatabase.Competency("Zulu");
        var optional = TestDatabase.Competency("Aardvark");
        context.AddRange(
            data.Department, data.Position, data.Requisition, alpha, zulu, optional,
            TestDatabase.JobRequisitionRequirement(data.Requisition, zulu),
            TestDatabase.JobRequisitionRequirement(data.Requisition, alpha),
            TestDatabase.JobRequisitionRequirement(data.Requisition, optional, false));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).ListByRequisitionAsync(data.Requisition.Id);

        Assert.Equal(["Alpha", "Zulu", "Aardvark"], result.Value.Select(item => item.CompetencyName));
        Assert.Equal(alpha.Code, result.Value[0].CompetencyCode);
        Assert.Equal(alpha.CompetencyCategory, result.Value[0].CompetencyCategory);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Get_MissingRequirement_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(
            await Service(context).GetByIdAsync(Guid.NewGuid()),
            "job_requisition_requirement_not_found",
            ErrorType.NotFound);
    }

    [Fact]
    public async Task Get_ExistingRequirement_ProjectsCompetencyCatalogValues()
    {
        await using var context = TestDatabase.CreateContext();
        var data = SeedData();
        var competency = TestDatabase.Competency("Architecture");
        competency.CompetencyCategory = CompetencyCategory.TechnicalConcept;
        var requirement = TestDatabase.JobRequisitionRequirement(data.Requisition, competency);
        context.AddRange(data.Department, data.Position, data.Requisition, competency, requirement);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).GetByIdAsync(requirement.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(competency.Code, result.Value.CompetencyCode);
        Assert.Equal(competency.Name, result.Value.CompetencyName);
        Assert.Equal(competency.CompetencyCategory, result.Value.CompetencyCategory);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Create_ValidRequest_TrimsNotesAndReturnsCatalogProjection()
    {
        await using var context = TestDatabase.CreateContext();
        var data = SeedData();
        var competency = TestDatabase.Competency("C#");
        context.AddRange(data.Department, data.Position, data.Requisition, competency);
        await context.SaveChangesAsync();

        var result = await Service(context).CreateAsync(
            new(data.Requisition.Id, competency.Id, 24, ProficiencyLevel.Advanced, true, "  Notes  "));

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal(data.Requisition.Id, result.Value.JobRequisitionId);
        Assert.Equal(competency.Id, result.Value.CompetencyId);
        Assert.Equal(competency.Code, result.Value.CompetencyCode);
        Assert.Equal("Notes", result.Value.Notes);
        Assert.Single(context.JobRequisitionRequirements);
    }

    [Fact]
    public async Task Create_MissingRequisition_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var competency = TestDatabase.Competency();
        context.Add(competency);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).CreateAsync(ValidCreate(Guid.NewGuid(), competency.Id)),
            "job_requisition_not_found",
            ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_MissingCompetency_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var data = SeedData();
        context.AddRange(data.Department, data.Position, data.Requisition);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).CreateAsync(ValidCreate(data.Requisition.Id, Guid.NewGuid())),
            "competency_not_found",
            ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_InactiveCompetency_ReturnsFailure()
    {
        await using var context = TestDatabase.CreateContext();
        var data = SeedData();
        var competency = TestDatabase.Competency(isActive: false);
        context.AddRange(data.Department, data.Position, data.Requisition, competency);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).CreateAsync(ValidCreate(data.Requisition.Id, competency.Id)),
            "competency_inactive",
            ErrorType.Failure);
    }

    [Fact]
    public async Task Create_DuplicateUniqueKey_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        var data = SeedData();
        var competency = TestDatabase.Competency();
        context.AddRange(
            data.Department, data.Position, data.Requisition, competency,
            TestDatabase.JobRequisitionRequirement(data.Requisition, competency));
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).CreateAsync(ValidCreate(data.Requisition.Id, competency.Id)),
            "job_requisition_requirement_conflict",
            ErrorType.Conflict);
    }

    [Theory]
    [InlineData(JobRequisitionStatus.Closed)]
    [InlineData(JobRequisitionStatus.Cancelled)]
    public async Task Create_TerminalRequisition_ReturnsLocked(JobRequisitionStatus status)
    {
        await using var context = TestDatabase.CreateContext();
        var data = SeedData(status);
        var competency = TestDatabase.Competency();
        context.AddRange(data.Department, data.Position, data.Requisition, competency);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).CreateAsync(ValidCreate(data.Requisition.Id, competency.Id)),
            "job_requisition_locked",
            ErrorType.Conflict);
    }

    [Fact]
    public async Task Update_ChangesOnlyMutableScalarsAndTrimsNotes()
    {
        await using var context = TestDatabase.CreateContext();
        var data = SeedData();
        var competency = TestDatabase.Competency();
        var requirement = TestDatabase.JobRequisitionRequirement(data.Requisition, competency);
        context.AddRange(data.Department, data.Position, data.Requisition, competency, requirement);
        await context.SaveChangesAsync();
        var requisitionId = requirement.JobRequisitionId;
        var competencyId = requirement.CompetencyId;

        var result = await Service(context).UpdateAsync(
            requirement.Id, new(60, ProficiencyLevel.Expert, false, "  Updated  "));

        Assert.True(result.IsSuccess);
        Assert.Equal(requisitionId, result.Value.JobRequisitionId);
        Assert.Equal(competencyId, result.Value.CompetencyId);
        Assert.Equal(60, result.Value.MinimumExperienceMonths);
        Assert.Equal(ProficiencyLevel.Expert, result.Value.MinimumProficiencyLevel);
        Assert.False(result.Value.IsRequired);
        Assert.Equal("Updated", result.Value.Notes);
    }

    [Fact]
    public async Task Update_MissingRequirement_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(
            await Service(context).UpdateAsync(Guid.NewGuid(), new(null, null, false, null)),
            "job_requisition_requirement_not_found",
            ErrorType.NotFound);
    }

    [Fact]
    public async Task Update_InvalidRequest_ReturnsValidationWithoutMutation()
    {
        await using var context = TestDatabase.CreateContext();
        var data = SeedData();
        var competency = TestDatabase.Competency();
        var requirement = TestDatabase.JobRequisitionRequirement(data.Requisition, competency);
        context.AddRange(data.Department, data.Position, data.Requisition, competency, requirement);
        await context.SaveChangesAsync();
        var originalMonths = requirement.MinimumExperienceMonths;
        var originalLevel = requirement.MinimumProficiencyLevel;
        var originalRequired = requirement.IsRequired;
        var originalNotes = requirement.Notes;

        var result = await Service(context).UpdateAsync(
            requirement.Id,
            new(-1, (ProficiencyLevel)999, !originalRequired, " "));

        Assert.True(result.IsFailure);
        Assert.All(result.Errors, error => Assert.Equal(ErrorType.Validation, error.Type));
        Assert.Equal(originalMonths, requirement.MinimumExperienceMonths);
        Assert.Equal(originalLevel, requirement.MinimumProficiencyLevel);
        Assert.Equal(originalRequired, requirement.IsRequired);
        Assert.Equal(originalNotes, requirement.Notes);
    }

    [Theory]
    [InlineData(JobRequisitionStatus.Closed)]
    [InlineData(JobRequisitionStatus.Cancelled)]
    public async Task UpdateAndDelete_TerminalRequisitionReturnLocked(JobRequisitionStatus status)
    {
        await using var context = TestDatabase.CreateContext();
        var data = SeedData(status);
        var competency = TestDatabase.Competency();
        var requirement = TestDatabase.JobRequisitionRequirement(data.Requisition, competency);
        context.AddRange(data.Department, data.Position, data.Requisition, competency, requirement);
        await context.SaveChangesAsync();
        var service = Service(context);
        AssertError(
            await service.UpdateAsync(requirement.Id, new(null, null, false, null)),
            "job_requisition_locked",
            ErrorType.Conflict);
        AssertError(
            await service.DeleteAsync(requirement.Id),
            "job_requisition_locked",
            ErrorType.Conflict);
    }

    [Fact]
    public async Task Delete_RemovesOnlyRequirementAndPreservesRequisitionAndCatalogs()
    {
        await using var context = TestDatabase.CreateContext();
        var data = SeedData();
        var competency = TestDatabase.Competency();
        var requirement = TestDatabase.JobRequisitionRequirement(data.Requisition, competency);
        context.AddRange(data.Department, data.Position, data.Requisition, competency, requirement);
        await context.SaveChangesAsync();

        Assert.True((await Service(context).DeleteAsync(requirement.Id)).IsSuccess);
        Assert.Empty(context.JobRequisitionRequirements);
        Assert.Single(context.JobRequisitions);
        Assert.Single(context.Departments);
        Assert.Single(context.Positions);
        Assert.Single(context.Competencies);
    }

    [Fact]
    public async Task Delete_MissingRequirement_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(
            await Service(context).DeleteAsync(Guid.NewGuid()),
            "job_requisition_requirement_not_found",
            ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_InvalidRequest_ReturnsMultipleValidationErrors()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).CreateAsync(
            new(Guid.Empty, Guid.Empty, -1, (ProficiencyLevel)999, true, "   "));
        Assert.Equal(5, result.Errors.Count);
        Assert.All(result.Errors, error => Assert.Equal(ErrorType.Validation, error.Type));
    }

    private static JobRequisitionRequirementService Service(HrDecisionSupportDbContext context) =>
        new(
            context,
            new CreateJobRequisitionRequirementRequestValidator(),
            new UpdateJobRequisitionRequirementRequestValidator());

    private static CreateJobRequisitionRequirementRequest ValidCreate(
        Guid requisitionId,
        Guid competencyId) =>
        new(requisitionId, competencyId, 12, ProficiencyLevel.Intermediate, true, null);

    private static Seed SeedData(JobRequisitionStatus status = JobRequisitionStatus.Draft)
    {
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        DateOnly? closedAt = status is JobRequisitionStatus.Closed or JobRequisitionStatus.Cancelled
            ? new DateOnly(2026, 2, 1)
            : null;
        return new(
            department,
            position,
            TestDatabase.JobRequisition(department, position, status: status, closedAt: closedAt));
    }

    private static void AssertError(Result result, string code, ErrorType type)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(code, Assert.Single(result.Errors).Code);
        Assert.Equal(type, result.Error!.Type);
    }

    private sealed record Seed(
        HrDecisionSupport.Domain.Entities.Department Department,
        HrDecisionSupport.Domain.Entities.Position Position,
        HrDecisionSupport.Domain.Entities.JobRequisition Requisition);
}
