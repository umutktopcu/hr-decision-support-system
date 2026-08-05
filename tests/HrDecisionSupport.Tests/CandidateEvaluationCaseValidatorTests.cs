using HrDecisionSupport.Application.CandidateEvaluations;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Tests;

public class CandidateEvaluationCaseValidatorTests
{
    private static readonly DateTime ValidReceivedAtUtc =
        new(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CreateValidator_ValidRequest_IsValid()
    {
        var result = new CreateCandidateEvaluationCaseRequestValidator().Validate(
            new(Guid.NewGuid(), Guid.NewGuid(), "REF", ValidReceivedAtUtc, "Notes"));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateValidator_MultipleInvalidFields_ReturnsAllMachineReadableErrors()
    {
        var result = new CreateCandidateEvaluationCaseRequestValidator().Validate(
            new(Guid.Empty, Guid.Empty, " ", default, " "));
        AssertErrors(
            result.Errors,
            ("candidate_id_required", "CandidateId"),
            ("job_requisition_id_required", "JobRequisitionId"),
            ("external_reference_whitespace", "ExternalReference"),
            ("received_at_utc_required", "ReceivedAtUtc"),
            ("notes_whitespace", "Notes"));
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void CreateValidator_ExternalReferenceHonorsConfigurationBoundary(int length, bool valid)
    {
        var result = new CreateCandidateEvaluationCaseRequestValidator().Validate(
            new(Guid.NewGuid(), Guid.NewGuid(), new string('R', length), ValidReceivedAtUtc, null));
        Assert.Equal(valid, result.IsValid);
        if (!valid)
            AssertErrors(result.Errors, ("external_reference_max_length", "ExternalReference"));
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void CreateValidator_NotesHonorsConfigurationBoundary(int length, bool valid)
    {
        var result = new CreateCandidateEvaluationCaseRequestValidator().Validate(
            new(Guid.NewGuid(), Guid.NewGuid(), null, ValidReceivedAtUtc, new string('N', length)));
        Assert.Equal(valid, result.IsValid);
        if (!valid)
            AssertErrors(result.Errors, ("notes_max_length", "Notes"));
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void UpdateValidator_ExternalReferenceHonorsConfigurationBoundary(int length, bool valid)
    {
        var result = new UpdateCandidateEvaluationCaseRequestValidator().Validate(
            new(new string('R', length), ValidReceivedAtUtc, null));
        Assert.Equal(valid, result.IsValid);
        if (!valid)
            AssertErrors(result.Errors, ("external_reference_max_length", "ExternalReference"));
    }

    [Theory]
    [InlineData(2000, true)]
    [InlineData(2001, false)]
    public void UpdateValidator_NotesHonorsConfigurationBoundary(int length, bool valid)
    {
        var result = new UpdateCandidateEvaluationCaseRequestValidator().Validate(
            new(null, ValidReceivedAtUtc, new string('N', length)));
        Assert.Equal(valid, result.IsValid);
        if (!valid)
            AssertErrors(result.Errors, ("notes_max_length", "Notes"));
    }

    [Fact]
    public void UpdateValidator_ValidRequest_IsValid()
    {
        var result = new UpdateCandidateEvaluationCaseRequestValidator().Validate(
            new(null, ValidReceivedAtUtc, null));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateValidator_DefaultReceivedDate_ReturnsCorrectValidationError()
    {
        var result = new UpdateCandidateEvaluationCaseRequestValidator().Validate(
            new(null, default, null));
        AssertErrors(result.Errors, ("received_at_utc_required", "ReceivedAtUtc"));
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Validators_NonUtcReceivedDate_ReturnCorrectValidationError(DateTimeKind kind)
    {
        var received = DateTime.SpecifyKind(new DateTime(2026, 7, 1), kind);
        var create = new CreateCandidateEvaluationCaseRequestValidator().Validate(
            new(Guid.NewGuid(), Guid.NewGuid(), null, received, null));
        var update = new UpdateCandidateEvaluationCaseRequestValidator().Validate(
            new(null, received, null));
        AssertErrors(create.Errors, ("received_at_utc_invalid", "ReceivedAtUtc"));
        AssertErrors(update.Errors, ("received_at_utc_invalid", "ReceivedAtUtc"));
    }

    [Fact]
    public void UpdateRequest_DoesNotExposeIdentityForeignKeysStatusOrAuditFields()
    {
        var properties = typeof(UpdateCandidateEvaluationCaseRequest)
            .GetProperties()
            .Select(item => item.Name)
            .ToArray();
        Assert.DoesNotContain("Id", properties);
        Assert.DoesNotContain("CandidateId", properties);
        Assert.DoesNotContain("JobRequisitionId", properties);
        Assert.DoesNotContain("Status", properties);
        Assert.DoesNotContain("CreatedAtUtc", properties);
        Assert.DoesNotContain("UpdatedAtUtc", properties);
    }

    [Fact]
    public void CreateRequest_DoesNotExposeStatusOrAuditFields()
    {
        var properties = typeof(CreateCandidateEvaluationCaseRequest)
            .GetProperties()
            .Select(item => item.Name)
            .ToArray();
        Assert.DoesNotContain("Status", properties);
        Assert.DoesNotContain("CreatedAtUtc", properties);
        Assert.DoesNotContain("UpdatedAtUtc", properties);
    }

    [Fact]
    public void StatusValidator_AllDefinedValuesAreValid()
    {
        var validator = new ChangeCandidateEvaluationCaseStatusRequestValidator();
        Assert.All(
            Enum.GetValues<CandidateEvaluationStatus>(),
            status => Assert.True(validator.Validate(new(status)).IsValid));
    }

    [Fact]
    public void StatusValidator_InvalidEnum_ReturnsCorrectValidationError()
    {
        var result = new ChangeCandidateEvaluationCaseStatusRequestValidator().Validate(
            new((CandidateEvaluationStatus)999));
        AssertErrors(
            result.Errors,
            ("candidate_evaluation_case_status_invalid", "Status"));
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
