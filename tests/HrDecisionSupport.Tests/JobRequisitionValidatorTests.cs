using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Requisitions;
using HrDecisionSupport.Application.Requisitions.Requirements;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Tests;

public class JobRequisitionValidatorTests
{
    private static CreateJobRequisitionRequest ValidCreate() =>
        new("R", "T", Guid.NewGuid(), Guid.NewGuid(), null, 1, null, new(2026, 1, 1), null, 0.5m, null, Guid.NewGuid(), true, new[] { new JobRequirementModel(Guid.NewGuid(), null, null, true, null) });

    [Fact]
    public void CreateValidator_ValidRequest_IsValid()
    {
        var result = new CreateJobRequisitionRequestValidator().Validate(ValidCreate());
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateValidator_MultipleInvalidFields_ReturnsMachineReadableErrorsAndProperties()
    {
        var result = new CreateJobRequisitionRequestValidator().Validate(
            new(" ", "", Guid.Empty, Guid.Empty, " ", 0, null, default, null));
        AssertErrors(
            result.Errors,
            ("requisition_code_required", "RequisitionCode"),
            ("title_required", "Title"),
            ("description_whitespace", "Description"),
            ("department_id_required", "DepartmentId"),
            ("position_id_required", "PositionId"),
            ("openings_count_invalid", "OpeningsCount"),
            ("opened_at_required", "OpenedAt"),
            ("work_mode_required", "WorkModeId"),
            ("mandatory_skill_required", "Requirements"));
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void CreateValidator_RequisitionCodeLengthHonorsConfigurationBoundary(int length, bool valid)
    {
        var result = new CreateJobRequisitionRequestValidator().Validate(
            ValidCreate() with { RequisitionCode = new string('R', length) });
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.Contains(result.Errors, error => error.Code == "requisition_code_max_length");
    }

    [Theory]
    [InlineData(250, true)]
    [InlineData(251, false)]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void CreateValidator_TitleAndDescriptionLengthsHonorConfigurationBoundaries(int length, bool valid)
    {
        var title = length <= 251 ? new string('T', length) : "T";
        var description = length >= 2000 ? new string('D', length) : null;
        var result = new CreateJobRequisitionRequestValidator().Validate(
            ValidCreate() with { Title = title, Description = description });
        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void CreateValidator_DuplicateLanguageRequirements_ReturnsValidationError()
    {
        var languageId = Guid.NewGuid();
        var request = ValidCreate() with
        {
            LanguageRequirements = new[]
            {
                new JobLanguageRequirementModel(languageId, LanguageProficiencyLevel.B2, true),
                new JobLanguageRequirementModel(languageId, LanguageProficiencyLevel.C1, false)
            }
        };

        var result = new CreateJobRequisitionRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "duplicate_language_requirement");
    }

    [Fact]
    public void UpdateValidator_DuplicateLanguageRequirements_ReturnsValidationError()
    {
        var languageId = Guid.NewGuid();
        var request = new UpdateJobRequisitionRequest(
            "REQ-UPDATED", "Title", Guid.NewGuid(), Guid.NewGuid(), null, 1, null, new DateOnly(2026, 1, 1),
            null, null, Guid.NewGuid(), true,
            new[] { new JobRequirementModel(Guid.NewGuid(), null, null, true, null) },
            new[]
            {
                new JobLanguageRequirementModel(languageId, LanguageProficiencyLevel.B2, true),
                new JobLanguageRequirementModel(languageId, LanguageProficiencyLevel.C1, false)
            });

        var result = new UpdateJobRequisitionRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Code == "duplicate_language_requirement");
    }

    [Fact]
    public void CreateValidator_DifferentLanguageRequirements_IsValid()
    {
        var request = ValidCreate() with
        {
            LanguageRequirements = new[]
            {
                new JobLanguageRequirementModel(Guid.NewGuid(), LanguageProficiencyLevel.B2, true),
                new JobLanguageRequirementModel(Guid.NewGuid(), LanguageProficiencyLevel.C1, false)
            }
        };

        var result = new CreateJobRequisitionRequestValidator().Validate(request);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateRequest_DoesNotExposeIdStatusOrTimestamps()
    {
        var properties = typeof(UpdateJobRequisitionRequest).GetProperties().Select(item => item.Name).ToArray();
        Assert.DoesNotContain("Id", properties);
        Assert.DoesNotContain("JobRequisitionStatus", properties);
        Assert.DoesNotContain("Status", properties);
        Assert.DoesNotContain("ClosedAt", properties);
        Assert.DoesNotContain("CreatedAtUtc", properties);
        Assert.DoesNotContain("UpdatedAtUtc", properties);
    }

    [Fact]
    public void StatusValidator_InvalidEnum_IsValidationErrorWithCorrectProperty()
    {
        var result = new ChangeJobRequisitionStatusRequestValidator().Validate(new((JobRequisitionStatus)999, null));
        AssertErrors(result.Errors, ("job_requisition_status_invalid", "Status"));
    }

    [Theory]
    [InlineData(JobRequisitionStatus.Closed)]
    [InlineData(JobRequisitionStatus.Cancelled)]
    public void StatusValidator_TerminalStatusRequiresClosedDate(JobRequisitionStatus status)
    {
        var result = new ChangeJobRequisitionStatusRequestValidator().Validate(new(status, null));
        AssertErrors(result.Errors, ("closed_at_required", "ClosedAt"));
    }

    [Theory]
    [InlineData(JobRequisitionStatus.Draft)]
    [InlineData(JobRequisitionStatus.Open)]
    [InlineData(JobRequisitionStatus.OnHold)]
    public void StatusValidator_NonTerminalStatusRejectsClosedDate(JobRequisitionStatus status)
    {
        var result = new ChangeJobRequisitionStatusRequestValidator().Validate(new(status, new(2026, 1, 1)));
        AssertErrors(result.Errors, ("closed_at_not_allowed", "ClosedAt"));
    }

    [Fact]
    public void RequirementCreateValidator_ValidRequest_IsValid()
    {
        var result = new CreateJobRequisitionRequirementRequestValidator().Validate(
            new(Guid.NewGuid(), Guid.NewGuid(), 0, CompetencyProficiencyLevel.Beginner, false, null));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void RequirementCreateValidator_MultipleInvalidFields_ReturnsAllErrors()
    {
        var result = new CreateJobRequisitionRequirementRequestValidator().Validate(
            new(Guid.Empty, Guid.Empty, -1, (CompetencyProficiencyLevel)999, true, " "));
        AssertErrors(
            result.Errors,
            ("job_requisition_id_required", "JobRequisitionId"),
            ("competency_id_required", "CompetencyId"),
            ("minimum_experience_months_negative", "MinimumExperienceMonths"),
            ("minimum_proficiency_level_invalid", "MinimumProficiencyLevel"),
            ("notes_whitespace", "Notes"));
    }

    [Theory]
    [InlineData(1000, true)]
    [InlineData(1001, false)]
    public void RequirementValidator_NotesLengthHonorsConfigurationBoundary(int length, bool valid)
    {
        var result = new UpdateJobRequisitionRequirementRequestValidator().Validate(
            new(0, CompetencyProficiencyLevel.Beginner, true, new string('N', length)));
        Assert.Equal(valid, result.IsValid);
        if (!valid) Assert.Contains(result.Errors, error => error.Code == "notes_max_length");
    }

    [Fact]
    public void RequirementUpdateRequest_DoesNotExposeIdentityOrImmutableForeignKeys()
    {
        var properties = typeof(UpdateJobRequisitionRequirementRequest)
            .GetProperties().Select(item => item.Name).ToArray();
        Assert.DoesNotContain("Id", properties);
        Assert.DoesNotContain("JobRequisitionId", properties);
        Assert.DoesNotContain("CompetencyId", properties);
    }

    private static void AssertErrors(
        IReadOnlyList<ValidationError> actual,
        params (string Code, string Property)[] expected)
    {
        Assert.Equal(expected.Length, actual.Count);
        foreach (var item in expected)
        {
            var error = Assert.Single(actual, value => value.Code == item.Code);
            Assert.Equal(item.Property, error.PropertyName);
            Assert.Equal(ErrorType.Validation, error.Type);
        }
    }
}
