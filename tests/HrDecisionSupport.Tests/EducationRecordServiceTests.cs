using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Profiles.Education;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

public class EducationRecordServiceTests
{
    [Fact]
    public async Task List_MissingPerson_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(await Service(context).ListByPersonAsync(Guid.NewGuid()), "person_not_found");
    }

    [Fact]
    public async Task List_ExistingPersonWithoutRecords_ReturnsEmptyList()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("EDU-EMPTY");
        context.Add(person); await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.True(result.IsSuccess); Assert.Empty(result.Value);
    }

    [Fact]
    public async Task Create_MissingPerson_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(await Service(context).CreateAsync(Create(Guid.NewGuid())), "person_not_found");
    }

    [Fact]
    public async Task Create_ValidRequest_TrimsAndPersistsRecord()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("EDU-CREATE");
        context.Add(person); await context.SaveChangesAsync();
        var result = await Service(context).CreateAsync(Create(person.Id) with
            { Institution = "  University  ", FieldOfStudy = "  Computing  ", GraduationDate = null });
        Assert.True(result.IsSuccess); Assert.Equal("University", result.Value.Institution);
        Assert.Equal("Computing", result.Value.FieldOfStudy); Assert.Null(result.Value.GraduationDate);
    }

    [Theory]
    [InlineData(" ", DegreeLevel.Bachelor, "institution_required")]
    [InlineData("University", (DegreeLevel)500, "degree_level_invalid")]
    public async Task Create_InvalidRequiredOrEnum_ReturnsValidation(string institution, DegreeLevel level, string code)
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).CreateAsync(Create(Guid.NewGuid()) with
            { Institution = institution, DegreeLevel = level });
        Assert.Contains(result.Errors, error => error.Code == code && error.Type == ErrorType.Validation);
    }

    [Fact]
    public async Task Create_GraduationBeforeStart_ReturnsValidation()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).CreateAsync(Create(Guid.NewGuid()) with
        { StartDate = new(2024, 1, 1), GraduationDate = new(2023, 1, 1) });
        Assert.Contains(result.Errors, error => error.Code == "graduation_date_before_start_date");
    }

    [Fact]
    public async Task List_OrdersDatedRecordsDescendingAndNullDatesLast()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("EDU-ORDER");
        context.AddRange(person,
            Record(person, "Old", new(2010, 1, 1), new(2014, 1, 1)),
            Record(person, "Current", new(2024, 1, 1), null),
            Record(person, "New", new(2018, 1, 1), new(2022, 1, 1)));
        await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.Equal(["New", "Old", "Current"], result.Value.Select(item => item.Institution));
    }

    [Fact]
    public async Task Update_ChangesFieldsButPreservesPersonId()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("EDU-UPD");
        var record = Record(person, "Old", null, null); context.AddRange(person, record); await context.SaveChangesAsync();
        var result = await Service(context).UpdateAsync(record.Id,
            new(" New ", " Field ", DegreeLevel.Master, new(2020, 1, 1), null));
        Assert.True(result.IsSuccess); Assert.Equal(person.Id, result.Value.PersonId);
        Assert.Equal("New", result.Value.Institution); Assert.Equal(DegreeLevel.Master, result.Value.DegreeLevel);
    }

    [Fact]
    public async Task Delete_RemovesOnlyEducationRecord()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("EDU-DEL");
        var record = Record(person, "Delete", null, null); context.AddRange(person, record); await context.SaveChangesAsync();
        Assert.True((await Service(context).DeleteAsync(record.Id)).IsSuccess);
        Assert.Empty(context.EducationRecords); Assert.Single(context.People);
    }

    [Fact]
    public async Task GetUpdateDelete_MissingRecord_ReturnNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var service = Service(context); var id = Guid.NewGuid();
        AssertError(await service.GetByIdAsync(id), "education_record_not_found");
        AssertError(await service.UpdateAsync(id, new("U", null, DegreeLevel.Other, null, null)), "education_record_not_found");
        AssertError(await service.DeleteAsync(id), "education_record_not_found");
    }

    private static CreateEducationRecordRequest Create(Guid personId) =>
        new(personId, "University", "Computing", DegreeLevel.Bachelor,
            new(2020, 1, 1), new(2024, 1, 1));
    private static EducationRecord Record(Person person, string institution, DateOnly? start, DateOnly? graduation) => new()
    {
        Id = Guid.NewGuid(), PersonId = person.Id, Person = person, Institution = institution,
        DegreeLevel = DegreeLevel.Bachelor, StartDate = start, GraduationDate = graduation
    };
    private static EducationRecordService Service(HrDecisionSupportDbContext context) =>
        new(context, new CreateEducationRecordRequestValidator(), new UpdateEducationRecordRequestValidator());
    private static void AssertError(Result result, string code)
    { Assert.True(result.IsFailure); Assert.Equal(code, result.Error!.Code); Assert.Equal(ErrorType.NotFound, result.Error.Type); }
}
