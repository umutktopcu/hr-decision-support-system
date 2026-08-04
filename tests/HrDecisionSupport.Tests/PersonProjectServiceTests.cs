using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Profiles.Projects;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;

namespace HrDecisionSupport.Tests;

public class PersonProjectServiceTests
{
    [Fact]
    public async Task List_ExistingPersonWithoutProjects_ReturnsEmptyList()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("PROJECT-EMPTY");
        context.Add(person); await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.True(result.IsSuccess); Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListAndCreate_MissingPerson_ReturnNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var service = Service(context); var personId = Guid.NewGuid();
        AssertNotFound(await service.ListByPersonAsync(personId), "person_not_found");
        AssertNotFound(await service.CreateAsync(Create(personId, Guid.NewGuid())), "person_not_found");
    }

    [Fact]
    public async Task Create_MissingProject_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("PROJECT-NOT-FOUND");
        context.Add(person); await context.SaveChangesAsync();
        AssertNotFound(await Service(context).CreateAsync(Create(person.Id, Guid.NewGuid())), "project_not_found");
    }

    [Fact]
    public async Task Create_ValidRequest_TrimsPersistsAndAllowsNullEndDate()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("PROJECT-CREATE");
        var project = TestDatabase.Project("Apollo"); context.AddRange(person, project); await context.SaveChangesAsync();
        var result = await Service(context).CreateAsync(Create(person.Id, project.Id) with
        { Role = "  Developer  ", Description = "  Details  ", EndDate = null });
        Assert.True(result.IsSuccess); Assert.Equal("Apollo", result.Value.ProjectName);
        Assert.Equal("Developer", result.Value.Role); Assert.Equal("Details", result.Value.Description);
        Assert.Null(result.Value.EndDate); Assert.Single(context.PersonProjects);
    }

    [Fact]
    public async Task Create_EndBeforeStart_ReturnsValidation()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).CreateAsync(Create(Guid.NewGuid(), Guid.NewGuid()) with
        { StartDate = new(2024, 1, 1), EndDate = new(2023, 1, 1) });
        Assert.Contains(result.Errors, error => error.Code == "end_date_before_start_date");
    }

    [Fact]
    public async Task Create_DuplicatePersonProject_IsAllowedByConfiguration()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("PROJECT-DUP");
        var project = TestDatabase.Project(); context.AddRange(person, project); await context.SaveChangesAsync();
        var service = Service(context);
        Assert.True((await service.CreateAsync(Create(person.Id, project.Id))).IsSuccess);
        Assert.True((await service.CreateAsync(Create(person.Id, project.Id))).IsSuccess);
        Assert.Equal(2, context.PersonProjects.Count());
    }

    [Fact]
    public async Task List_OrdersOngoingFirstThenStartProjectNameAndId()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("PROJECT-ORDER");
        var alpha = TestDatabase.Project("Alpha"); var beta = TestDatabase.Project("Beta");
        var noStart = TestDatabase.Project("No Start"); var dated = TestDatabase.Project("Dated");
        context.AddRange(person, alpha, beta, noStart, dated,
            Link(person, beta, Guid.NewGuid(), new(2024, 1, 1), null),
            Link(person, alpha, Guid.NewGuid(), new(2024, 1, 1), null),
            Link(person, noStart, Guid.NewGuid(), null, null),
            Link(person, dated, Guid.NewGuid(), new(2025, 1, 1), new(2026, 1, 1)));
        await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.Equal(["Alpha", "Beta", "No Start", "Dated"], result.Value.Select(item => item.ProjectName));
    }

    [Fact]
    public async Task Update_ChangesMetadataAndPreservesRelationshipIds()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("PROJECT-UPD");
        var project = TestDatabase.Project(); var link = Link(person, project, Guid.NewGuid(), null, null);
        context.AddRange(person, project, link); await context.SaveChangesAsync();
        var result = await Service(context).UpdateAsync(link.Id,
            new(" Lead ", new(2020, 1, 1), null, " Notes "));
        Assert.True(result.IsSuccess); Assert.Equal(person.Id, result.Value.PersonId);
        Assert.Equal(project.Id, result.Value.ProjectId); Assert.Equal("Lead", result.Value.Role);
    }

    [Fact]
    public async Task Delete_RemovesOnlyLinkAndPreservesPersonAndProject()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("PROJECT-DEL");
        var project = TestDatabase.Project(); var link = Link(person, project, Guid.NewGuid(), null, null);
        context.AddRange(person, project, link); await context.SaveChangesAsync();
        Assert.True((await Service(context).DeleteAsync(link.Id)).IsSuccess);
        Assert.Empty(context.PersonProjects); Assert.Single(context.People); Assert.Single(context.Projects);
    }

    [Fact]
    public async Task GetUpdateDelete_MissingLink_ReturnNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var service = Service(context); var id = Guid.NewGuid();
        AssertNotFound(await service.GetByIdAsync(id), "person_project_not_found");
        AssertNotFound(await service.UpdateAsync(id, new(null, null, null, null)), "person_project_not_found");
        AssertNotFound(await service.DeleteAsync(id), "person_project_not_found");
    }

    private static CreatePersonProjectRequest Create(Guid personId, Guid projectId) =>
        new(personId, projectId, "Developer", new(2020, 1, 1), new(2022, 1, 1), "Description");
    private static PersonProject Link(Person person, Project project, Guid id, DateOnly? start, DateOnly? end) =>
        new() { Id = id, PersonId = person.Id, Person = person, ProjectId = project.Id, Project = project,
            Role = "Role", StartDate = start, EndDate = end };
    private static PersonProjectService Service(HrDecisionSupportDbContext context) =>
        new(context, new CreatePersonProjectRequestValidator(), new UpdatePersonProjectRequestValidator());
    private static void AssertNotFound(Result result, string code)
    { Assert.True(result.IsFailure); Assert.Equal(code, result.Error!.Code); Assert.Equal(ErrorType.NotFound, result.Error.Type); }
}
