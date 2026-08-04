using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Profiles.EmploymentHistory;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;

namespace HrDecisionSupport.Tests;

public class EmploymentHistoryServiceTests
{
    [Fact]
    public async Task List_ExistingPersonWithoutHistory_ReturnsEmptyList()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("EMP-HIST-EMPTY");
        context.Add(person); await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.True(result.IsSuccess); Assert.Empty(result.Value);
    }

    [Fact]
    public async Task List_MissingPerson_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertNotFound(await Service(context).ListByPersonAsync(Guid.NewGuid()), "person_not_found");
    }

    [Fact]
    public async Task Create_ValidRequest_TrimsPersistsAndAllowsNullEndDate()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("EMP-HIST-CREATE");
        context.Add(person); await context.SaveChangesAsync();
        var result = await Service(context).CreateAsync(Create(person.Id) with
        { EmployerName = "  Employer  ", PositionTitle = "  Engineer  ", Description = "  Details  ", EndDate = null });
        Assert.True(result.IsSuccess); Assert.Equal("Employer", result.Value.EmployerName);
        Assert.Equal("Engineer", result.Value.PositionTitle); Assert.Equal("Details", result.Value.Description);
        Assert.Null(result.Value.EndDate); Assert.Single(context.EmploymentHistories);
    }

    [Fact]
    public async Task Create_MissingPerson_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertNotFound(await Service(context).CreateAsync(Create(Guid.NewGuid())), "person_not_found");
    }

    [Theory]
    [InlineData(" ", "Engineer", "employer_name_required")]
    [InlineData("Employer", " ", "position_title_required")]
    public async Task Create_RequiredWhitespace_ReturnsValidation(string employer, string position, string code)
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).CreateAsync(Create(Guid.NewGuid()) with
        { EmployerName = employer, PositionTitle = position });
        Assert.Contains(result.Errors, error => error.Code == code && error.Type == ErrorType.Validation);
    }

    [Fact]
    public async Task Create_EndBeforeStart_ReturnsValidation()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).CreateAsync(Create(Guid.NewGuid()) with
        { StartDate = new(2024, 1, 1), EndDate = new(2023, 1, 1) });
        Assert.Contains(result.Errors, error => error.Code == "end_date_before_start_date");
    }

    [Fact]
    public async Task List_OrdersOngoingFirstThenStartDescendingAndId()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("EMP-HIST-ORDER");
        var firstId = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var secondId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        context.AddRange(person,
            History(person, secondId, "Current B", new(2024, 1, 1), null),
            History(person, firstId, "Current A", new(2024, 1, 1), null),
            History(person, Guid.NewGuid(), "Recent", new(2023, 1, 1), new(2024, 1, 1)),
            History(person, Guid.NewGuid(), "Old", new(2010, 1, 1), new(2015, 1, 1)));
        await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.Equal(["Current A", "Current B", "Recent", "Old"], result.Value.Select(item => item.EmployerName));
    }

    [Fact]
    public async Task Update_ChangesFieldsAndPreservesPersonId()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("EMP-HIST-UPD");
        var history = History(person, Guid.NewGuid(), "Old", new(2020, 1, 1), null);
        context.AddRange(person, history); await context.SaveChangesAsync();
        var result = await Service(context).UpdateAsync(history.Id,
            new(" New ", " Lead ", new(2021, 1, 1), null, " Notes "));
        Assert.True(result.IsSuccess); Assert.Equal(person.Id, result.Value.PersonId);
        Assert.Equal("New", result.Value.EmployerName); Assert.Equal("Lead", result.Value.PositionTitle);
    }

    [Fact]
    public async Task Delete_RemovesOnlyHistory()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("EMP-HIST-DEL");
        var history = History(person, Guid.NewGuid(), "Delete", new(2020, 1, 1), null);
        context.AddRange(person, history); await context.SaveChangesAsync();
        Assert.True((await Service(context).DeleteAsync(history.Id)).IsSuccess);
        Assert.Empty(context.EmploymentHistories); Assert.Single(context.People);
    }

    [Fact]
    public async Task GetUpdateDelete_MissingHistory_ReturnNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var service = Service(context); var id = Guid.NewGuid();
        AssertNotFound(await service.GetByIdAsync(id), "employment_history_not_found");
        AssertNotFound(await service.UpdateAsync(id, new("E", "P", new(2020, 1, 1), null, null)),
            "employment_history_not_found");
        AssertNotFound(await service.DeleteAsync(id), "employment_history_not_found");
    }

    private static CreateEmploymentHistoryRequest Create(Guid personId) =>
        new(personId, "Employer", "Engineer", new(2020, 1, 1), new(2022, 1, 1), "Description");
    private static EmploymentHistory History(Person person, Guid id, string employer, DateOnly start, DateOnly? end) =>
        new() { Id = id, PersonId = person.Id, Person = person, EmployerName = employer,
            PositionTitle = "Engineer", StartDate = start, EndDate = end };
    private static EmploymentHistoryService Service(HrDecisionSupportDbContext context) =>
        new(context, new CreateEmploymentHistoryRequestValidator(), new UpdateEmploymentHistoryRequestValidator());
    private static void AssertNotFound(Result result, string code)
    { Assert.True(result.IsFailure); Assert.Equal(code, result.Error!.Code); Assert.Equal(ErrorType.NotFound, result.Error.Type); }
}
