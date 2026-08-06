using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Profiles.Languages;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

public class PersonLanguageServiceTests
{
    [Fact]
    public async Task List_ExistingPersonWithoutLanguages_ReturnsEmptyList()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("LNG-EMPTY");
        context.Add(person); await context.SaveChangesAsync();
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
    public async Task Create_MissingPerson_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var language = TestDatabase.Language();
        context.Add(language); await context.SaveChangesAsync();
        AssertError(await Service(context).CreateAsync(Create(Guid.NewGuid(), language.Id)), "person_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_MissingLanguage_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("LNG-NOCAT");
        context.Add(person); await context.SaveChangesAsync();
        AssertError(await Service(context).CreateAsync(Create(person.Id, Guid.NewGuid())), "language_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_DuplicateLanguage_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("LNG-DUP");
        var language = TestDatabase.Language(); context.AddRange(person, language, Link(person, language)); await context.SaveChangesAsync();
        AssertError(await Service(context).CreateAsync(Create(person.Id, language.Id)), "person_language_conflict", ErrorType.Conflict);
    }

    [Fact]
    public async Task Create_InvalidLevel_ReturnsValidation()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).CreateAsync(Create(Guid.NewGuid(), Guid.NewGuid()) with
            { ProficiencyLevel = (LanguageProficiencyLevel)500 });
        AssertError(result, "proficiency_level_invalid", ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAndUpdate_PersistIsNative()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("LNG-NATIVE");
        var language = TestDatabase.Language(); context.AddRange(person, language); await context.SaveChangesAsync();
        var service = Service(context); var created = await service.CreateAsync(Create(person.Id, language.Id) with { IsNative = true });
        Assert.True(created.Value.IsNative);
        var updated = await service.UpdateAsync(created.Value.Id, new(LanguageProficiencyLevel.C2, false));
        Assert.False(updated.Value.IsNative); Assert.Equal(LanguageProficiencyLevel.C2, updated.Value.ProficiencyLevel);
    }

    [Fact]
    public async Task List_OrdersByLanguageName()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("LNG-ORDER");
        var zulu = TestDatabase.Language("Zulu"); var alpha = TestDatabase.Language("Alpha");
        context.AddRange(person, zulu, alpha, Link(person, zulu), Link(person, alpha)); await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.Equal(["Alpha", "Zulu"], result.Value.Select(item => item.LanguageName));
    }

    [Fact]
    public async Task Update_PreservesPersonAndLanguageIds()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("LNG-UPD");
        var language = TestDatabase.Language(); var link = Link(person, language);
        context.AddRange(person, language, link); await context.SaveChangesAsync();
        var result = await Service(context).UpdateAsync(link.Id, new(LanguageProficiencyLevel.B2, true));
        Assert.Equal(person.Id, result.Value.PersonId); Assert.Equal(language.Id, result.Value.LanguageId);
    }

    [Fact]
    public async Task Delete_RemovesOnlyLink()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("LNG-DEL");
        var language = TestDatabase.Language(); var link = Link(person, language);
        context.AddRange(person, language, link); await context.SaveChangesAsync();
        Assert.True((await Service(context).DeleteAsync(link.Id)).IsSuccess);
        Assert.Empty(context.PersonLanguages); Assert.Single(context.Languages); Assert.Single(context.People);
    }

    [Fact]
    public async Task GetUpdateDelete_MissingLink_ReturnNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var service = Service(context); var id = Guid.NewGuid();
        AssertError(await service.GetByIdAsync(id), "person_language_not_found", ErrorType.NotFound);
        AssertError(await service.UpdateAsync(id, new(LanguageProficiencyLevel.A1, false)), "person_language_not_found", ErrorType.NotFound);
        AssertError(await service.DeleteAsync(id), "person_language_not_found", ErrorType.NotFound);
    }

    private static CreatePersonLanguageRequest Create(Guid personId, Guid languageId) =>
        new(personId, languageId, LanguageProficiencyLevel.B1, false);
    private static PersonLanguage Link(Person person, Language language) => new()
    {
        Id = Guid.NewGuid(), PersonId = person.Id, Person = person, LanguageId = language.Id,
        Language = language, ProficiencyLevel = LanguageProficiencyLevel.B1
    };
    private static PersonLanguageService Service(HrDecisionSupportDbContext context) =>
        new(context, new CreatePersonLanguageRequestValidator(), new UpdatePersonLanguageRequestValidator());
    private static void AssertError(Result result, string code, ErrorType type)
    { Assert.True(result.IsFailure); Assert.Equal(code, result.Error!.Code); Assert.Equal(type, result.Error.Type); }
}
