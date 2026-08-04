using HrDecisionSupport.Application.Profiles.Certificates;
using HrDecisionSupport.Application.Profiles.Competencies;
using HrDecisionSupport.Application.Profiles.Education;
using HrDecisionSupport.Application.Profiles.Languages;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Tests;

public class ProfileRequestValidatorTests
{
    [Fact]
    public void AllProfileValidators_ValidRequests_AreValid()
    {
        Assert.True(new CreatePersonCompetencyRequestValidator().Validate(CompetencyCreate()).IsValid);
        Assert.True(new UpdatePersonCompetencyRequestValidator().Validate(new(1, ProficiencyLevel.Beginner)).IsValid);
        Assert.True(new CreateEducationRecordRequestValidator().Validate(EducationCreate()).IsValid);
        Assert.True(new UpdateEducationRecordRequestValidator().Validate(EducationUpdate()).IsValid);
        Assert.True(new CreatePersonCertificateRequestValidator().Validate(CertificateCreate()).IsValid);
        Assert.True(new UpdatePersonCertificateRequestValidator().Validate(CertificateUpdate()).IsValid);
        Assert.True(new CreatePersonLanguageRequestValidator().Validate(LanguageCreate()).IsValid);
        Assert.True(new UpdatePersonLanguageRequestValidator().Validate(new(ProficiencyLevel.Advanced, true)).IsValid);
    }

    [Fact]
    public void CompetencyValidators_MultipleInvalidFields_ReturnMultipleErrors()
    {
        var create = new CreatePersonCompetencyRequestValidator().Validate(
            new(Guid.Empty, Guid.Empty, -1, (ProficiencyLevel)500));
        var update = new UpdatePersonCompetencyRequestValidator().Validate(new(-1, (ProficiencyLevel)500));
        Assert.Equal(4, create.Errors.Count); Assert.Equal(2, update.Errors.Count);
        Assert.Contains(create.Errors, error => error.PropertyName == "PersonId");
        Assert.Contains(create.Errors, error => error.PropertyName == "CompetencyId");
    }

    [Fact]
    public void EducationValidators_MultipleInvalidFields_ReturnMultipleErrors()
    {
        var create = new CreateEducationRecordRequestValidator().Validate(new(
            Guid.Empty, " ", " ", (DegreeLevel)500, new(2024, 1, 1), new(2023, 1, 1)));
        var update = new UpdateEducationRecordRequestValidator().Validate(new(
            " ", " ", (DegreeLevel)500, new(2024, 1, 1), new(2023, 1, 1)));
        Assert.Equal(5, create.Errors.Count); Assert.Equal(4, update.Errors.Count);
    }

    [Fact]
    public void CertificateValidators_MultipleInvalidFields_ReturnMultipleErrors()
    {
        var create = new CreatePersonCertificateRequestValidator().Validate(new(
            Guid.Empty, Guid.Empty, new(2024, 1, 1), new(2023, 1, 1), " "));
        var update = new UpdatePersonCertificateRequestValidator().Validate(new(
            new(2024, 1, 1), new(2023, 1, 1), " "));
        Assert.Equal(4, create.Errors.Count); Assert.Equal(2, update.Errors.Count);
    }

    [Fact]
    public void LanguageValidators_RejectEmptyIdsAndInvalidEnum()
    {
        var create = new CreatePersonLanguageRequestValidator().Validate(
            new(Guid.Empty, Guid.Empty, (ProficiencyLevel)500, false));
        var update = new UpdatePersonLanguageRequestValidator().Validate(new((ProficiencyLevel)500, true));
        Assert.Equal(3, create.Errors.Count); Assert.Single(update.Errors);
        Assert.All(create.Errors, error => Assert.NotEmpty(error.PropertyName));
    }

    [Theory]
    [InlineData(250, true)]
    [InlineData(251, false)]
    public void EducationValidators_EnforceInstitutionLength(int length, bool expectedValid)
    {
        var value = new string('I', length);
        Assert.Equal(expectedValid, new CreateEducationRecordRequestValidator()
            .Validate(EducationCreate() with { Institution = value }).IsValid);
        Assert.Equal(expectedValid, new UpdateEducationRecordRequestValidator()
            .Validate(EducationUpdate() with { Institution = value }).IsValid);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void EducationValidators_EnforceFieldOfStudyLength(int length, bool expectedValid)
    {
        var value = new string('F', length);
        Assert.Equal(expectedValid, new CreateEducationRecordRequestValidator()
            .Validate(EducationCreate() with { FieldOfStudy = value }).IsValid);
        Assert.Equal(expectedValid, new UpdateEducationRecordRequestValidator()
            .Validate(EducationUpdate() with { FieldOfStudy = value }).IsValid);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void CertificateValidators_EnforceCredentialCodeLength(int length, bool expectedValid)
    {
        var value = new string('C', length);
        Assert.Equal(expectedValid, new CreatePersonCertificateRequestValidator()
            .Validate(CertificateCreate() with { CredentialCode = value }).IsValid);
        Assert.Equal(expectedValid, new UpdatePersonCertificateRequestValidator()
            .Validate(CertificateUpdate() with { CredentialCode = value }).IsValid);
    }

    [Fact]
    public void DateValidators_AllowNullDatesAndRejectReverseOrder()
    {
        Assert.True(new CreateEducationRecordRequestValidator().Validate(
            EducationCreate() with { StartDate = null, GraduationDate = null }).IsValid);
        Assert.True(new CreatePersonCertificateRequestValidator().Validate(
            CertificateCreate() with { IssueDate = null, ExpirationDate = null }).IsValid);
        Assert.Contains(new UpdateEducationRecordRequestValidator().Validate(
            EducationUpdate() with { StartDate = new(2024, 1, 1), GraduationDate = new(2023, 1, 1) }).Errors,
            error => error.PropertyName == "GraduationDate");
        Assert.Contains(new UpdatePersonCertificateRequestValidator().Validate(
            CertificateUpdate() with { IssueDate = new(2024, 1, 1), ExpirationDate = new(2023, 1, 1) }).Errors,
            error => error.PropertyName == "ExpirationDate");
    }

    [Theory]
    [InlineData(typeof(UpdatePersonCompetencyRequest))]
    [InlineData(typeof(UpdateEducationRecordRequest))]
    [InlineData(typeof(UpdatePersonCertificateRequest))]
    [InlineData(typeof(UpdatePersonLanguageRequest))]
    public void UpdateRequests_DoNotExposeImmutableRelationshipIds(Type requestType)
    {
        var names = requestType.GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain("PersonId", names); Assert.DoesNotContain("CompetencyId", names);
        Assert.DoesNotContain("CertificateId", names); Assert.DoesNotContain("LanguageId", names);
    }

    private static CreatePersonCompetencyRequest CompetencyCreate() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 12, ProficiencyLevel.Advanced);
    private static CreateEducationRecordRequest EducationCreate() =>
        new(Guid.NewGuid(), "University", "Computing", DegreeLevel.Bachelor, new(2020, 1, 1), new(2024, 1, 1));
    private static UpdateEducationRecordRequest EducationUpdate() =>
        new("University", "Computing", DegreeLevel.Bachelor, new(2020, 1, 1), new(2024, 1, 1));
    private static CreatePersonCertificateRequest CertificateCreate() =>
        new(Guid.NewGuid(), Guid.NewGuid(), new(2020, 1, 1), new(2024, 1, 1), "code");
    private static UpdatePersonCertificateRequest CertificateUpdate() =>
        new(new(2020, 1, 1), new(2024, 1, 1), "code");
    private static CreatePersonLanguageRequest LanguageCreate() =>
        new(Guid.NewGuid(), Guid.NewGuid(), ProficiencyLevel.Advanced, false);
}
