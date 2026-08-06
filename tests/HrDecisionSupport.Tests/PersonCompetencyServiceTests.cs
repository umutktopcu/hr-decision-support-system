using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Profiles.Competencies;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

public class PersonCompetencyServiceTests
{
    [Fact]
    public async Task List_ExistingPersonWithoutItems_ReturnsSuccessfulEmptyList()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("PC-EMPTY");
        context.People.Add(person); await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.True(result.IsSuccess); Assert.Empty(result.Value);
    }

    [Fact]
    public async Task List_MissingPerson_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(await Service(context).ListByPersonAsync(Guid.NewGuid()), "person_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task List_OrdersByCompetencyName()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("PC-ORDER");
        var z = TestDatabase.Competency("Zulu"); var a = TestDatabase.Competency("Alpha");
        context.AddRange(person, z, a,
            Link(person, z), Link(person, a)); await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.Equal(["Alpha", "Zulu"], result.Value.Select(item => item.CompetencyName));
    }

    [Fact]
    public async Task Get_MissingItem_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(await Service(context).GetByIdAsync(Guid.NewGuid()), "person_competency_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_ValidRequest_CreatesLink()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("PC-CREATE"); var competency = TestDatabase.Competency();
        context.AddRange(person, competency); await context.SaveChangesAsync();
        var result = await Service(context).CreateAsync(new(person.Id, competency.Id, 12, CompetencyProficiencyLevel.Advanced));
        Assert.True(result.IsSuccess);
        var stored = await context.PersonCompetencies.SingleAsync();
        Assert.Equal(12, stored.ExperienceMonths); Assert.Equal(CompetencyProficiencyLevel.Advanced, stored.ProficiencyLevel);
    }

    [Fact]
    public async Task Create_MissingPerson_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var competency = TestDatabase.Competency(); context.Add(competency); await context.SaveChangesAsync();
        AssertError(await Service(context).CreateAsync(new(Guid.NewGuid(), competency.Id, null, null)), "person_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_MissingCompetency_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("PC-NOCOMP"); context.Add(person); await context.SaveChangesAsync();
        AssertError(await Service(context).CreateAsync(new(person.Id, Guid.NewGuid(), null, null)), "competency_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_InactiveCompetency_ReturnsFailure()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("PC-INACTIVE"); var competency = TestDatabase.Competency(isActive: false);
        context.AddRange(person, competency); await context.SaveChangesAsync();
        AssertError(await Service(context).CreateAsync(new(person.Id, competency.Id, null, null)), "competency_inactive", ErrorType.Failure);
    }

    [Fact]
    public async Task Create_DuplicateLink_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("PC-DUP"); var competency = TestDatabase.Competency();
        context.AddRange(person, competency, Link(person, competency)); await context.SaveChangesAsync();
        AssertError(await Service(context).CreateAsync(new(person.Id, competency.Id, null, null)), "person_competency_conflict", ErrorType.Conflict);
    }

    [Theory]
    [InlineData(-1, CompetencyProficiencyLevel.Beginner, "experience_months_negative")]
    [InlineData(1, (CompetencyProficiencyLevel)500, "proficiency_level_invalid")]
    public async Task Create_InvalidMetadata_ReturnsValidation(int months, CompetencyProficiencyLevel level, string code)
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).CreateAsync(new(Guid.NewGuid(), Guid.NewGuid(), months, level));
        Assert.Contains(result.Errors, error => error.Code == code && error.Type == ErrorType.Validation);
    }

    [Fact]
    public async Task Update_ChangesOnlyMetadata()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("PC-UPD"); var competency = TestDatabase.Competency();
        var link = Link(person, competency); context.AddRange(person, competency, link); await context.SaveChangesAsync();
        var personId = link.PersonId; var competencyId = link.CompetencyId;
        var result = await Service(context).UpdateAsync(link.Id, new(48, CompetencyProficiencyLevel.Expert));
        Assert.True(result.IsSuccess); Assert.Equal(personId, result.Value.PersonId); Assert.Equal(competencyId, result.Value.CompetencyId);
        Assert.Equal(48, result.Value.ExperienceMonths); Assert.Equal(CompetencyProficiencyLevel.Expert, result.Value.ProficiencyLevel);
    }

    [Fact]
    public async Task Delete_RemovesOnlyLink()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("PC-DEL"); var competency = TestDatabase.Competency();
        var link = Link(person, competency); context.AddRange(person, competency, link); await context.SaveChangesAsync();
        Assert.True((await Service(context).DeleteAsync(link.Id)).IsSuccess);
        Assert.Empty(context.PersonCompetencies); Assert.Single(context.People); Assert.Single(context.Competencies);
    }

    [Fact]
    public async Task UpdateAndDelete_MissingItem_ReturnNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var service = Service(context);
        AssertError(await service.UpdateAsync(Guid.NewGuid(), new(null, null)), "person_competency_not_found", ErrorType.NotFound);
        AssertError(await service.DeleteAsync(Guid.NewGuid()), "person_competency_not_found", ErrorType.NotFound);
    }

    private static PersonCompetencyService Service(Infrastructure.Persistence.HrDecisionSupportDbContext context) =>
        new(context, new CreatePersonCompetencyRequestValidator(), new UpdatePersonCompetencyRequestValidator());
    private static PersonCompetency Link(Person person, Competency competency) => new()
    {
        Id = Guid.NewGuid(), PersonId = person.Id, CompetencyId = competency.Id,
        Person = person, Competency = competency
    };
    private static void AssertError(Result result, string code, ErrorType type)
    { Assert.True(result.IsFailure); Assert.Equal(code, result.Error!.Code); Assert.Equal(type, result.Error.Type); }
}
