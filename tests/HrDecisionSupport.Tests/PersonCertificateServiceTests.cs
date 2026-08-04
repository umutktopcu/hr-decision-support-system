using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Profiles.Certificates;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

public class PersonCertificateServiceTests
{
    [Fact]
    public async Task List_ExistingPersonWithoutCertificates_ReturnsEmptyList()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("CRT-EMPTY");
        context.Add(person); await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.True(result.IsSuccess); Assert.Empty(result.Value);
    }

    [Fact]
    public async Task List_MissingPerson_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(await Service(context).ListByPersonAsync(Guid.NewGuid()), "person_not_found");
    }

    [Fact]
    public async Task Create_MissingPerson_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var certificate = TestDatabase.Certificate();
        context.Add(certificate); await context.SaveChangesAsync();
        AssertError(await Service(context).CreateAsync(Create(Guid.NewGuid(), certificate.Id)), "person_not_found");
    }

    [Fact]
    public async Task Create_MissingCertificate_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("CRT-NOCAT");
        context.Add(person); await context.SaveChangesAsync();
        AssertError(await Service(context).CreateAsync(Create(person.Id, Guid.NewGuid())), "certificate_not_found");
    }

    [Fact]
    public async Task Create_NullIssuerAndDates_MapsSuccessfully()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("CRT-NULL");
        var certificate = TestDatabase.Certificate(issuer: null); context.AddRange(person, certificate); await context.SaveChangesAsync();
        var result = await Service(context).CreateAsync(new(person.Id, certificate.Id, null, null, null));
        Assert.True(result.IsSuccess); Assert.Null(result.Value.Issuer); Assert.Null(result.Value.IssueDate);
        Assert.Null(result.Value.ExpirationDate);
    }

    [Fact]
    public async Task Create_SameCertificateTwice_IsAllowed()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("CRT-TWICE");
        var certificate = TestDatabase.Certificate(); context.AddRange(person, certificate); await context.SaveChangesAsync();
        var service = Service(context);
        Assert.True((await service.CreateAsync(Create(person.Id, certificate.Id) with { IssueDate = new(2020, 1, 1) })).IsSuccess);
        Assert.True((await service.CreateAsync(Create(person.Id, certificate.Id) with { IssueDate = new(2024, 1, 1) })).IsSuccess);
        Assert.Equal(2, await context.PersonCertificates.CountAsync());
    }

    [Theory]
    [InlineData(" ", "credential_code_whitespace")]
    [InlineData("valid", "expiration_date_before_issue_date")]
    public async Task Create_InvalidMetadata_ReturnsValidation(string credential, string code)
    {
        await using var context = TestDatabase.CreateContext();
        var request = Create(Guid.NewGuid(), Guid.NewGuid()) with { CredentialCode = credential };
        if (code == "expiration_date_before_issue_date")
            request = request with { IssueDate = new(2024, 1, 1), ExpirationDate = new(2023, 1, 1) };
        var result = await Service(context).CreateAsync(request);
        Assert.Contains(result.Errors, error => error.Code == code && error.Type == ErrorType.Validation);
    }

    [Fact]
    public async Task List_OrdersByIssueDateThenNameWithNullLast()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("CRT-ORDER");
        var alpha = TestDatabase.Certificate("Alpha"); var zulu = TestDatabase.Certificate("Zulu");
        context.AddRange(person, alpha, zulu,
            Link(person, alpha, new(2024, 1, 1)), Link(person, zulu, new(2024, 1, 1)),
            Link(person, alpha, null)); await context.SaveChangesAsync();
        var result = await Service(context).ListByPersonAsync(person.Id);
        Assert.Equal(["Alpha", "Zulu", "Alpha"], result.Value.Select(item => item.CertificateName));
        Assert.Null(result.Value[^1].IssueDate);
    }

    [Fact]
    public async Task Update_ChangesMetadataAndPreservesIds()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("CRT-UPD");
        var certificate = TestDatabase.Certificate(); var link = Link(person, certificate, null);
        context.AddRange(person, certificate, link); await context.SaveChangesAsync();
        var result = await Service(context).UpdateAsync(link.Id,
            new(new(2022, 1, 1), new(2025, 1, 1), " CODE "));
        Assert.True(result.IsSuccess); Assert.Equal(person.Id, result.Value.PersonId);
        Assert.Equal(certificate.Id, result.Value.CertificateId); Assert.Equal("CODE", result.Value.CredentialCode);
    }

    [Fact]
    public async Task Delete_RemovesOnlyLink()
    {
        await using var context = TestDatabase.CreateContext(); var person = TestDatabase.Person("CRT-DEL");
        var certificate = TestDatabase.Certificate(); var link = Link(person, certificate, null);
        context.AddRange(person, certificate, link); await context.SaveChangesAsync();
        Assert.True((await Service(context).DeleteAsync(link.Id)).IsSuccess);
        Assert.Empty(context.PersonCertificates); Assert.Single(context.Certificates); Assert.Single(context.People);
    }

    [Fact]
    public async Task GetUpdateDelete_MissingLink_ReturnNotFound()
    {
        await using var context = TestDatabase.CreateContext(); var service = Service(context); var id = Guid.NewGuid();
        AssertError(await service.GetByIdAsync(id), "person_certificate_not_found");
        AssertError(await service.UpdateAsync(id, new(null, null, null)), "person_certificate_not_found");
        AssertError(await service.DeleteAsync(id), "person_certificate_not_found");
    }

    private static CreatePersonCertificateRequest Create(Guid personId, Guid certificateId) =>
        new(personId, certificateId, new(2020, 1, 1), new(2025, 1, 1), "credential");
    private static PersonCertificate Link(Person person, Certificate certificate, DateOnly? issueDate) => new()
    {
        Id = Guid.NewGuid(), PersonId = person.Id, Person = person, CertificateId = certificate.Id,
        Certificate = certificate, IssueDate = issueDate
    };
    private static PersonCertificateService Service(HrDecisionSupportDbContext context) =>
        new(context, new CreatePersonCertificateRequestValidator(), new UpdatePersonCertificateRequestValidator());
    private static void AssertError(Result result, string code)
    { Assert.True(result.IsFailure); Assert.Equal(code, result.Error!.Code); Assert.Equal(ErrorType.NotFound, result.Error.Type); }
}
