using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Profiles.WorkModes;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;

namespace HrDecisionSupport.Tests;

public class PersonWorkModeExperienceServiceTests
{
    [Fact]
    public async Task List_ExistingPersonWithoutExperience_ReturnsEmptyList()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("WORK-MODE-EMPTY");
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
    public async Task Create_MissingWorkMode_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("WORK-MODE-NOT-FOUND");
        context.Add(person); await context.SaveChangesAsync();
        AssertNotFound(await Service(context).CreateAsync(Create(person.Id, Guid.NewGuid())), "work_mode_not_found");
    }

    [Fact]
    public async Task Create_ValidRequest_PersistsAndAllowsNullMonths()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("WORK-MODE-CREATE");
        var mode = TestDatabase.WorkMode("Hybrid"); context.AddRange(person, mode); await context.SaveChangesAsync();
        var result = await Service(context).CreateAsync(Create(person.Id, mode.Id) with { ExperienceMonths = null });
        Assert.True(result.IsSuccess); Assert.Equal(mode.Code, result.Value.WorkModeCode);
        Assert.Equal("Hybrid", result.Value.WorkModeName); Assert.Null(result.Value.ExperienceMonths);
        Assert.Single(context.PersonWorkModeExperiences);
    }

    [Fact]
    public async Task Create_NegativeMonths_ReturnsValidation()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).CreateAsync(Create(Guid.NewGuid(), Guid.NewGuid()) with { ExperienceMonths = -1 });
        Assert.Contains(result.Errors, error => error is ValidationError validationError
            && validationError.Code == "experience_months_negative"
            && validationError.PropertyName == "ExperienceMonths" && validationError.Type == ErrorType.Validation);
    }

    [Fact]
    public async Task Create_DuplicateRelationship_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("WORK-MODE-DUP");
        var mode = TestDatabase.WorkMode(); context.AddRange(person, mode); await context.SaveChangesAsync();
        var service = Service(context); Assert.True((await service.CreateAsync(Create(person.Id, mode.Id))).IsSuccess);
        var duplicate = await service.CreateAsync(Create(person.Id, mode.Id));
        Assert.True(duplicate.IsFailure); Assert.Equal("person_work_mode_experience_conflict", duplicate.Error!.Code);
        Assert.Equal(ErrorType.Conflict, duplicate.Error.Type);
    }

    [Fact]
    public async Task List_OrdersByWorkModeNameThenId()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("WORK-MODE-ORDER");
        var remote = TestDatabase.WorkMode("Remote"); var hybrid = TestDatabase.WorkMode("Hybrid");
        context.AddRange(person, remote, hybrid, Link(person, remote), Link(person, hybrid)); await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.Equal(["Hybrid", "Remote"], result.Value.Select(item => item.WorkModeName));
    }

    [Fact]
    public async Task Update_ChangesMonthsAndPreservesRelationshipIds()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("WORK-MODE-UPD");
        var mode = TestDatabase.WorkMode(); var link = Link(person, mode);
        context.AddRange(person, mode, link); await context.SaveChangesAsync();
        var result = await Service(context).UpdateAsync(link.Id, new(24));
        Assert.True(result.IsSuccess); Assert.Equal(person.Id, result.Value.PersonId);
        Assert.Equal(mode.Id, result.Value.WorkModeId); Assert.Equal(24, result.Value.ExperienceMonths);
    }

    [Fact]
    public async Task Delete_RemovesOnlyLinkAndPreservesPersonAndWorkMode()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("WORK-MODE-DEL");
        var mode = TestDatabase.WorkMode(); var link = Link(person, mode);
        context.AddRange(person, mode, link); await context.SaveChangesAsync();
        Assert.True((await Service(context).DeleteAsync(link.Id)).IsSuccess);
        Assert.Empty(context.PersonWorkModeExperiences); Assert.Single(context.People); Assert.Single(context.WorkModes);
    }

    [Fact]
    public async Task GetUpdateDelete_MissingLink_ReturnNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var service = Service(context); var id = Guid.NewGuid();
        AssertNotFound(await service.GetByIdAsync(id), "person_work_mode_experience_not_found");
        AssertNotFound(await service.UpdateAsync(id, new(null)), "person_work_mode_experience_not_found");
        AssertNotFound(await service.DeleteAsync(id), "person_work_mode_experience_not_found");
    }

    private static CreatePersonWorkModeExperienceRequest Create(Guid personId, Guid modeId) =>
        new(personId, modeId, 12);
    private static PersonWorkModeExperience Link(Person person, WorkMode mode) =>
        new() { Id = Guid.NewGuid(), PersonId = person.Id, Person = person, WorkModeId = mode.Id,
            WorkMode = mode, ExperienceMonths = 12 };
    private static PersonWorkModeExperienceService Service(HrDecisionSupportDbContext context) =>
        new(context, new CreatePersonWorkModeExperienceRequestValidator(), new UpdatePersonWorkModeExperienceRequestValidator());
    private static void AssertNotFound(Result result, string code)
    { Assert.True(result.IsFailure); Assert.Equal(code, result.Error!.Code); Assert.Equal(ErrorType.NotFound, result.Error.Type); }
}
