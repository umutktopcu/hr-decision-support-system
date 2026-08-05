using HrDecisionSupport.Application.Profiles.Certificates;
using HrDecisionSupport.Application.Profiles.Competencies;
using HrDecisionSupport.Application.Profiles.Education;
using HrDecisionSupport.Application.Profiles.EmploymentHistory;
using HrDecisionSupport.Application.Profiles.Languages;
using HrDecisionSupport.Application.Profiles.Projects;
using HrDecisionSupport.Application.Profiles.Sectors;
using HrDecisionSupport.Application.Profiles.WorkModes;
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
        Assert.True(new CreateEmploymentHistoryRequestValidator().Validate(EmploymentCreate()).IsValid);
        Assert.True(new UpdateEmploymentHistoryRequestValidator().Validate(EmploymentUpdate()).IsValid);
        Assert.True(new CreatePersonProjectRequestValidator().Validate(ProjectCreate()).IsValid);
        Assert.True(new UpdatePersonProjectRequestValidator().Validate(ProjectUpdate()).IsValid);
        Assert.True(new CreatePersonSectorExperienceRequestValidator().Validate(SectorCreate()).IsValid);
        Assert.True(new UpdatePersonSectorExperienceRequestValidator().Validate(new(12, "Notes")).IsValid);
        Assert.True(new CreatePersonWorkModeExperienceRequestValidator().Validate(WorkModeCreate()).IsValid);
        Assert.True(new UpdatePersonWorkModeExperienceRequestValidator().Validate(new(12)).IsValid);
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

    [Fact]
    public void EmploymentValidators_MultipleInvalidFields_ReturnAllErrors()
    {
        var create = new CreateEmploymentHistoryRequestValidator().Validate(new(
            Guid.Empty, " ", " ", new(2024, 1, 1), new(2023, 1, 1), " "));
        var update = new UpdateEmploymentHistoryRequestValidator().Validate(new(
            " ", " ", new(2024, 1, 1), new(2023, 1, 1), " "));
        Assert.Equal(5, create.Errors.Count);
        Assert.Equal(4, update.Errors.Count);
        Assert.Contains(create.Errors, error => error.Code == "person_id_required" && error.PropertyName == "PersonId");
        Assert.Contains(update.Errors, error => error.Code == "end_date_before_start_date" && error.PropertyName == "EndDate");
    }

    [Fact]
    public void ProjectValidators_MultipleInvalidFields_ReturnAllErrors()
    {
        var create = new CreatePersonProjectRequestValidator().Validate(new(
            Guid.Empty, Guid.Empty, " ", new(2024, 1, 1), new(2023, 1, 1), " "));
        var update = new UpdatePersonProjectRequestValidator().Validate(new(
            " ", new(2024, 1, 1), new(2023, 1, 1), " "));
        Assert.Equal(5, create.Errors.Count);
        Assert.Equal(3, update.Errors.Count);
        Assert.Contains(create.Errors, error => error.PropertyName == "ProjectId");
    }

    [Fact]
    public void ExperienceValidators_RejectEmptyIdsNegativeMonthsAndWhitespaceNotes()
    {
        var sector = new CreatePersonSectorExperienceRequestValidator().Validate(
            new(Guid.Empty, Guid.Empty, -1, " "));
        var workMode = new CreatePersonWorkModeExperienceRequestValidator().Validate(
            new(Guid.Empty, Guid.Empty, -1));
        Assert.Equal(4, sector.Errors.Count);
        Assert.Equal(3, workMode.Errors.Count);
        Assert.Contains(sector.Errors, error => error.Code == "experience_months_negative" && error.PropertyName == "ExperienceMonths");
        Assert.Contains(workMode.Errors, error => error.Code == "work_mode_id_required" && error.PropertyName == "WorkModeId");
    }

    [Fact]
    public void ExperienceValidators_AllowNullMonths()
    {
        Assert.True(new CreatePersonSectorExperienceRequestValidator()
            .Validate(SectorCreate() with { ExperienceMonths = null }).IsValid);
        Assert.True(new UpdatePersonSectorExperienceRequestValidator().Validate(new(null, null)).IsValid);
        Assert.True(new CreatePersonWorkModeExperienceRequestValidator()
            .Validate(WorkModeCreate() with { ExperienceMonths = null }).IsValid);
        Assert.True(new UpdatePersonWorkModeExperienceRequestValidator().Validate(new(null)).IsValid);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UpdateExperienceValidators_RejectNegativeMonths(bool isSectorExperience)
    {
        var result = isSectorExperience
            ? new UpdatePersonSectorExperienceRequestValidator().Validate(new(-1, null))
            : new UpdatePersonWorkModeExperienceRequestValidator().Validate(new(-1));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("experience_months_negative", error.Code);
        Assert.Equal("ExperienceMonths", error.PropertyName);
    }

    [Fact]
    public void UpdateSectorExperienceValidator_RejectsWhitespaceNotes()
    {
        var result = new UpdatePersonSectorExperienceRequestValidator().Validate(new(null, " "));

        Assert.False(result.IsValid);
        var error = Assert.Single(result.Errors);
        Assert.Equal("notes_whitespace", error.Code);
        Assert.Equal("Notes", error.PropertyName);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void EmploymentValidators_EnforceRequiredStringLengths(int length, bool expectedValid)
    {
        var value = new string('E', length);
        Assert.Equal(expectedValid, new CreateEmploymentHistoryRequestValidator()
            .Validate(EmploymentCreate() with { EmployerName = value, PositionTitle = value }).IsValid);
        Assert.Equal(expectedValid, new UpdateEmploymentHistoryRequestValidator()
            .Validate(EmploymentUpdate() with { EmployerName = value, PositionTitle = value }).IsValid);
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void EmploymentValidators_EnforceDescriptionLength(int length, bool expectedValid)
    {
        var value = new string('D', length);
        Assert.Equal(expectedValid, new CreateEmploymentHistoryRequestValidator()
            .Validate(EmploymentCreate() with { Description = value }).IsValid);
        Assert.Equal(expectedValid, new UpdateEmploymentHistoryRequestValidator()
            .Validate(EmploymentUpdate() with { Description = value }).IsValid);
    }

    [Theory]
    [InlineData(200, 2000, true)]
    [InlineData(201, 2000, false)]
    [InlineData(200, 2001, false)]
    public void ProjectValidators_EnforceRoleAndDescriptionLengths(int roleLength, int descriptionLength,
        bool expectedValid)
    {
        var role = new string('R', roleLength); var description = new string('D', descriptionLength);
        Assert.Equal(expectedValid, new CreatePersonProjectRequestValidator()
            .Validate(ProjectCreate() with { Role = role, Description = description }).IsValid);
        Assert.Equal(expectedValid, new UpdatePersonProjectRequestValidator()
            .Validate(ProjectUpdate() with { Role = role, Description = description }).IsValid);
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void SectorValidators_EnforceNotesLength(int length, bool expectedValid)
    {
        var notes = new string('N', length);
        Assert.Equal(expectedValid, new CreatePersonSectorExperienceRequestValidator()
            .Validate(SectorCreate() with { Notes = notes }).IsValid);
        Assert.Equal(expectedValid, new UpdatePersonSectorExperienceRequestValidator()
            .Validate(new(12, notes)).IsValid);
    }

    [Theory]
    [InlineData(typeof(UpdatePersonCompetencyRequest))]
    [InlineData(typeof(UpdateEducationRecordRequest))]
    [InlineData(typeof(UpdatePersonCertificateRequest))]
    [InlineData(typeof(UpdatePersonLanguageRequest))]
    [InlineData(typeof(UpdateEmploymentHistoryRequest))]
    [InlineData(typeof(UpdatePersonProjectRequest))]
    [InlineData(typeof(UpdatePersonSectorExperienceRequest))]
    [InlineData(typeof(UpdatePersonWorkModeExperienceRequest))]
    public void UpdateRequests_DoNotExposeImmutableRelationshipIds(Type requestType)
    {
        var names = requestType.GetProperties().Select(property => property.Name).ToArray();
        Assert.DoesNotContain("PersonId", names); Assert.DoesNotContain("CompetencyId", names);
        Assert.DoesNotContain("CertificateId", names); Assert.DoesNotContain("LanguageId", names);
        Assert.DoesNotContain("ProjectId", names); Assert.DoesNotContain("SectorId", names);
        Assert.DoesNotContain("WorkModeId", names);
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
    private static CreateEmploymentHistoryRequest EmploymentCreate() =>
        new(Guid.NewGuid(), "Employer", "Engineer", new(2020, 1, 1), null, "Description");
    private static UpdateEmploymentHistoryRequest EmploymentUpdate() =>
        new("Employer", "Engineer", new(2020, 1, 1), null, "Description");
    private static CreatePersonProjectRequest ProjectCreate() =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Developer", new(2020, 1, 1), null, "Description");
    private static UpdatePersonProjectRequest ProjectUpdate() =>
        new("Developer", new(2020, 1, 1), null, "Description");
    private static CreatePersonSectorExperienceRequest SectorCreate() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 12, "Notes");
    private static CreatePersonWorkModeExperienceRequest WorkModeCreate() =>
        new(Guid.NewGuid(), Guid.NewGuid(), 12);
}
