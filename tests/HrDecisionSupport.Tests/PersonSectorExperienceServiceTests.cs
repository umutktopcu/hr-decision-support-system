using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Profiles.Sectors;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;

namespace HrDecisionSupport.Tests;

public class PersonSectorExperienceServiceTests
{
    [Fact]
    public async Task List_ExistingPersonWithoutExperience_ReturnsEmptyList()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("SECTOR-EMPTY");
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
    public async Task Create_MissingSector_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("SECTOR-NOT-FOUND");
        context.Add(person); await context.SaveChangesAsync();
        AssertNotFound(await Service(context).CreateAsync(Create(person.Id, Guid.NewGuid())), "sector_not_found");
    }

    [Fact]
    public async Task Create_ValidRequest_TrimsPersistsAndAllowsNullMonths()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("SECTOR-CREATE");
        var sector = TestDatabase.Sector("Finance"); context.AddRange(person, sector); await context.SaveChangesAsync();
        var result = await Service(context).CreateAsync(Create(person.Id, sector.Id) with
        { ExperienceMonths = null, Notes = "  Details  " });
        Assert.True(result.IsSuccess); Assert.Equal(sector.Code, result.Value.SectorCode);
        Assert.Equal("Finance", result.Value.SectorName); Assert.Null(result.Value.ExperienceMonths);
        Assert.Equal("Details", result.Value.Notes);
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
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("SECTOR-DUP");
        var sector = TestDatabase.Sector(); context.AddRange(person, sector); await context.SaveChangesAsync();
        var service = Service(context); Assert.True((await service.CreateAsync(Create(person.Id, sector.Id))).IsSuccess);
        var duplicate = await service.CreateAsync(Create(person.Id, sector.Id));
        Assert.True(duplicate.IsFailure); Assert.Equal("person_sector_experience_conflict", duplicate.Error!.Code);
        Assert.Equal(ErrorType.Conflict, duplicate.Error.Type);
    }

    [Fact]
    public async Task List_OrdersBySectorNameThenId()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("SECTOR-ORDER");
        var zulu = TestDatabase.Sector("Zulu"); var alpha = TestDatabase.Sector("Alpha");
        context.AddRange(person, zulu, alpha, Link(person, zulu), Link(person, alpha)); await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.Equal(["Alpha", "Zulu"], result.Value.Select(item => item.SectorName));
    }

    [Fact]
    public async Task Update_ChangesMetadataAndPreservesRelationshipIds()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("SECTOR-UPD");
        var sector = TestDatabase.Sector(); var link = Link(person, sector);
        context.AddRange(person, sector, link); await context.SaveChangesAsync();
        var result = await Service(context).UpdateAsync(link.Id, new(24, " Notes "));
        Assert.True(result.IsSuccess); Assert.Equal(person.Id, result.Value.PersonId);
        Assert.Equal(sector.Id, result.Value.SectorId); Assert.Equal(24, result.Value.ExperienceMonths);
        Assert.Equal("Notes", result.Value.Notes);
    }

    [Fact]
    public async Task Delete_RemovesOnlyLinkAndPreservesPersonAndSector()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("SECTOR-DEL");
        var sector = TestDatabase.Sector(); var link = Link(person, sector);
        context.AddRange(person, sector, link); await context.SaveChangesAsync();
        Assert.True((await Service(context).DeleteAsync(link.Id)).IsSuccess);
        Assert.Empty(context.PersonSectorExperiences); Assert.Single(context.People); Assert.Single(context.Sectors);
    }

    [Fact]
    public async Task GetUpdateDelete_MissingLink_ReturnNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var service = Service(context); var id = Guid.NewGuid();
        AssertNotFound(await service.GetByIdAsync(id), "person_sector_experience_not_found");
        AssertNotFound(await service.UpdateAsync(id, new(null, null)), "person_sector_experience_not_found");
        AssertNotFound(await service.DeleteAsync(id), "person_sector_experience_not_found");
    }

    private static CreatePersonSectorExperienceRequest Create(Guid personId, Guid sectorId) =>
        new(personId, sectorId, 12, "Notes");
    private static PersonSectorExperience Link(Person person, Sector sector) =>
        new() { Id = Guid.NewGuid(), PersonId = person.Id, Person = person, SectorId = sector.Id,
            Sector = sector, ExperienceMonths = 12 };
    private static PersonSectorExperienceService Service(HrDecisionSupportDbContext context) =>
        new(context, new CreatePersonSectorExperienceRequestValidator(), new UpdatePersonSectorExperienceRequestValidator());
    private static void AssertNotFound(Result result, string code)
    { Assert.True(result.IsFailure); Assert.Equal(code, result.Error!.Code); Assert.Equal(ErrorType.NotFound, result.Error.Type); }
}
