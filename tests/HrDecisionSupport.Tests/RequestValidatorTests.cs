using HrDecisionSupport.Application.Candidates;
using HrDecisionSupport.Application.Candidates.Dtos;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.Employees.Dtos;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Tests;

public class RequestValidatorTests
{
    [Fact]
    public void CreateEmployeeValidator_ValidRequest_IsValid() =>
        Assert.True(new CreateEmployeeRequestValidator().Validate(ValidCreateEmployee()).IsValid);

    [Fact]
    public void UpdateEmployeeValidator_ValidRequest_IsValid() =>
        Assert.True(new UpdateEmployeeRequestValidator().Validate(ValidUpdateEmployee()).IsValid);

    [Fact]
    public void CreateCandidateValidator_ValidRequest_IsValid() =>
        Assert.True(new CreateCandidateRequestValidator().Validate(ValidCreateCandidate()).IsValid);

    [Fact]
    public void UpdateCandidateValidator_ValidRequest_IsValid() =>
        Assert.True(new UpdateCandidateRequestValidator().Validate(ValidUpdateCandidate()).IsValid);

    [Fact]
    public void CreateEmployeeValidator_MultipleInvalidFields_ReturnsMultipleErrors()
    {
        var request = ValidCreateEmployee() with
        {
            EmployeeCode = " ",
            AnonymousCode = " ",
            Email = "not-an-email",
            PhoneNumber = new string('x', 31),
            HireDate = default,
            EmploymentStatus = (EmploymentStatus)500,
            InitialDepartmentId = Guid.Empty,
            InitialPositionId = Guid.Empty
        };

        var result = new CreateEmployeeRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 7);
        Assert.Contains(result.Errors, error => error.PropertyName == "EmployeeCode");
        Assert.Contains(result.Errors, error => error.PropertyName == "AnonymousCode");
    }

    [Fact]
    public void UpdateEmployeeValidator_MultipleInvalidFields_ReturnsMultipleErrors()
    {
        var request = ValidUpdateEmployee() with
        {
            EmployeeCode = " ",
            FirstName = " ",
            Email = "invalid",
            HireDate = default,
            EmploymentStatus = (EmploymentStatus)500
        };

        var result = new UpdateEmployeeRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 5);
    }

    [Fact]
    public void CreateCandidateValidator_MultipleInvalidFields_ReturnsMultipleErrors()
    {
        var request = ValidCreateCandidate() with
        {
            CandidateCode = " ",
            AnonymousCode = " ",
            LastName = " ",
            Email = "invalid",
            CandidateSource = (CandidateSource)500,
            ExternalCandidateId = new string('x', 201)
        };

        var result = new CreateCandidateRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 6);
    }

    [Fact]
    public void UpdateCandidateValidator_MultipleInvalidFields_ReturnsMultipleErrors()
    {
        var request = ValidUpdateCandidate() with
        {
            CandidateCode = " ",
            FirstName = " ",
            Email = "invalid",
            CandidateSource = (CandidateSource)500,
            ExternalCandidateId = new string('x', 201)
        };

        var result = new UpdateCandidateRequestValidator().Validate(request);

        Assert.False(result.IsValid);
        Assert.True(result.Errors.Count >= 5);
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void EmployeeValidators_EnforceEmployeeCodeMaxLength(int length, bool expectedValid)
    {
        var code = new string('E', length);

        Assert.Equal(
            expectedValid,
            new CreateEmployeeRequestValidator()
                .Validate(ValidCreateEmployee() with { EmployeeCode = code }).IsValid);
        Assert.Equal(
            expectedValid,
            new UpdateEmployeeRequestValidator()
                .Validate(ValidUpdateEmployee() with { EmployeeCode = code }).IsValid);
    }

    [Theory]
    [InlineData(50, true)]
    [InlineData(51, false)]
    public void CandidateValidators_EnforceCandidateCodeMaxLength(int length, bool expectedValid)
    {
        var code = new string('C', length);

        Assert.Equal(
            expectedValid,
            new CreateCandidateRequestValidator()
                .Validate(ValidCreateCandidate() with { CandidateCode = code }).IsValid);
        Assert.Equal(
            expectedValid,
            new UpdateCandidateRequestValidator()
                .Validate(ValidUpdateCandidate() with { CandidateCode = code }).IsValid);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void CreateValidators_EnforceAnonymousCodeMaxLength(int length, bool expectedValid)
    {
        var code = new string('A', length);

        Assert.Equal(
            expectedValid,
            new CreateEmployeeRequestValidator()
                .Validate(ValidCreateEmployee() with { AnonymousCode = code }).IsValid);
        Assert.Equal(
            expectedValid,
            new CreateCandidateRequestValidator()
                .Validate(ValidCreateCandidate() with { AnonymousCode = code }).IsValid);
    }

    [Theory]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void CandidateValidators_EnforceExternalCandidateIdMaxLength(int length, bool expectedValid)
    {
        var externalId = new string('X', length);

        Assert.Equal(
            expectedValid,
            new CreateCandidateRequestValidator()
                .Validate(ValidCreateCandidate() with { ExternalCandidateId = externalId }).IsValid);
        Assert.Equal(
            expectedValid,
            new UpdateCandidateRequestValidator()
                .Validate(ValidUpdateCandidate() with { ExternalCandidateId = externalId }).IsValid);
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void AllValidators_EnforceFirstNameMaxLengthBoundary(int length, bool expectedValid)
    {
        var value = new string('F', length);
        var results = new[]
        {
            new CreateEmployeeRequestValidator().Validate(
                ValidCreateEmployee() with { FirstName = value }),
            new UpdateEmployeeRequestValidator().Validate(
                ValidUpdateEmployee() with { FirstName = value }),
            new CreateCandidateRequestValidator().Validate(
                ValidCreateCandidate() with { FirstName = value }),
            new UpdateCandidateRequestValidator().Validate(
                ValidUpdateCandidate() with { FirstName = value })
        };

        Assert.All(results, result => Assert.Equal(expectedValid, result.IsValid));
    }

    [Theory]
    [InlineData(100, true)]
    [InlineData(101, false)]
    public void AllValidators_EnforceLastNameMaxLengthBoundary(int length, bool expectedValid)
    {
        var value = new string('L', length);
        var results = new[]
        {
            new CreateEmployeeRequestValidator().Validate(
                ValidCreateEmployee() with { LastName = value }),
            new UpdateEmployeeRequestValidator().Validate(
                ValidUpdateEmployee() with { LastName = value }),
            new CreateCandidateRequestValidator().Validate(
                ValidCreateCandidate() with { LastName = value }),
            new UpdateCandidateRequestValidator().Validate(
                ValidUpdateCandidate() with { LastName = value })
        };

        Assert.All(results, result => Assert.Equal(expectedValid, result.IsValid));
    }

    [Theory]
    [InlineData(30, true)]
    [InlineData(31, false)]
    public void AllValidators_EnforcePhoneNumberMaxLengthBoundary(int length, bool expectedValid)
    {
        var value = new string('1', length);
        var results = new[]
        {
            new CreateEmployeeRequestValidator().Validate(
                ValidCreateEmployee() with { PhoneNumber = value }),
            new UpdateEmployeeRequestValidator().Validate(
                ValidUpdateEmployee() with { PhoneNumber = value }),
            new CreateCandidateRequestValidator().Validate(
                ValidCreateCandidate() with { PhoneNumber = value }),
            new UpdateCandidateRequestValidator().Validate(
                ValidUpdateCandidate() with { PhoneNumber = value })
        };

        Assert.All(results, result => Assert.Equal(expectedValid, result.IsValid));
    }

    [Theory]
    [InlineData(320, true)]
    [InlineData(321, false)]
    public void AllValidators_EnforceEmailMaxLengthBoundaryWithoutFormatError(
        int length,
        bool expectedValid)
    {
        var value = BoundaryEmail(length);
        var results = new[]
        {
            new CreateEmployeeRequestValidator().Validate(
                ValidCreateEmployee() with { Email = value }),
            new UpdateEmployeeRequestValidator().Validate(
                ValidUpdateEmployee() with { Email = value }),
            new CreateCandidateRequestValidator().Validate(
                ValidCreateCandidate() with { Email = value }),
            new UpdateCandidateRequestValidator().Validate(
                ValidUpdateCandidate() with { Email = value })
        };

        Assert.Equal(length, value.Length);
        Assert.All(results, result =>
        {
            Assert.Equal(expectedValid, result.IsValid);
            Assert.DoesNotContain(result.Errors, error => error.Code == "email_invalid");

            if (!expectedValid)
            {
                Assert.Contains(result.Errors, error => error.Code == "email_max_length");
            }
        });
    }

    [Fact]
    public void CreateValidators_RejectWhitespaceOnlyRequiredCodes()
    {
        var employee = new CreateEmployeeRequestValidator().Validate(
            ValidCreateEmployee() with { EmployeeCode = " ", AnonymousCode = "\t" });
        var candidate = new CreateCandidateRequestValidator().Validate(
            ValidCreateCandidate() with { CandidateCode = " ", AnonymousCode = "\t" });

        Assert.Equal(2, employee.Errors.Count(error =>
            error.PropertyName is "EmployeeCode" or "AnonymousCode"));
        Assert.Equal(2, candidate.Errors.Count(error =>
            error.PropertyName is "CandidateCode" or "AnonymousCode"));
    }

    [Fact]
    public void UpdateValidators_RejectWhitespaceOnlyRequiredCodes()
    {
        Assert.Contains(
            new UpdateEmployeeRequestValidator()
                .Validate(ValidUpdateEmployee() with { EmployeeCode = " " }).Errors,
            error => error.PropertyName == "EmployeeCode");
        Assert.Contains(
            new UpdateCandidateRequestValidator()
                .Validate(ValidUpdateCandidate() with { CandidateCode = " " }).Errors,
            error => error.PropertyName == "CandidateCode");
    }

    [Fact]
    public void EmployeeValidators_RejectUndefinedEmploymentStatus()
    {
        Assert.Contains(
            new CreateEmployeeRequestValidator().Validate(
                ValidCreateEmployee() with { EmploymentStatus = (EmploymentStatus)500 }).Errors,
            error => error.Code == "employment_status_invalid");
        Assert.Contains(
            new UpdateEmployeeRequestValidator().Validate(
                ValidUpdateEmployee() with { EmploymentStatus = (EmploymentStatus)500 }).Errors,
            error => error.Code == "employment_status_invalid");
    }

    [Fact]
    public void CandidateValidators_RejectUndefinedCandidateSource()
    {
        Assert.Contains(
            new CreateCandidateRequestValidator().Validate(
                ValidCreateCandidate() with { CandidateSource = (CandidateSource)500 }).Errors,
            error => error.Code == "candidate_source_invalid");
        Assert.Contains(
            new UpdateCandidateRequestValidator().Validate(
                ValidUpdateCandidate() with { CandidateSource = (CandidateSource)500 }).Errors,
            error => error.Code == "candidate_source_invalid");
    }

    [Fact]
    public void AllValidators_RejectInvalidEmail()
    {
        var results = new[]
        {
            new CreateEmployeeRequestValidator().Validate(
                ValidCreateEmployee() with { Email = "invalid" }),
            new UpdateEmployeeRequestValidator().Validate(
                ValidUpdateEmployee() with { Email = "invalid" }),
            new CreateCandidateRequestValidator().Validate(
                ValidCreateCandidate() with { Email = "invalid" }),
            new UpdateCandidateRequestValidator().Validate(
                ValidUpdateCandidate() with { Email = "invalid" })
        };

        Assert.All(results, result =>
            Assert.Contains(result.Errors, error => error.Code == "email_invalid"));
    }

    [Fact]
    public void EmployeeValidators_EnforceTerminationAndHireDateRules()
    {
        var create = new CreateEmployeeRequestValidator().Validate(
            ValidCreateEmployee() with
            {
                EmploymentStatus = EmploymentStatus.Terminated,
                TerminationDate = new DateOnly(2023, 12, 31)
            });
        var update = new UpdateEmployeeRequestValidator().Validate(
            ValidUpdateEmployee() with
            {
                EmploymentStatus = EmploymentStatus.Terminated,
                TerminationDate = null
            });

        Assert.Contains(create.Errors, error => error.Code == "termination_date_before_hire_date");
        Assert.Contains(update.Errors, error => error.Code == "termination_date_required");
    }

    [Fact]
    public void CreateEmployeeValidator_RejectsAssignmentStartBeforeHireDate()
    {
        var result = new CreateEmployeeRequestValidator().Validate(
            ValidCreateEmployee() with
            {
                InitialAssignmentStartDate = new DateOnly(2023, 12, 31)
            });

        Assert.Contains(
            result.Errors,
            error => error.Code == "initial_assignment_start_date_before_hire_date");
    }

    private static CreateEmployeeRequest ValidCreateEmployee() =>
        new(
            "EMP-01", "ANON-01", "Ada", "Lovelace", "ada@example.com", "+90-555",
            new DateOnly(2024, 1, 1), null, EmploymentStatus.Active,
            Guid.NewGuid(), Guid.NewGuid(), null);

    private static UpdateEmployeeRequest ValidUpdateEmployee() =>
        new(
            "EMP-01", "Ada", "Lovelace", "ada@example.com", "+90-555",
            new DateOnly(2024, 1, 1), null, EmploymentStatus.Active);

    private static CreateCandidateRequest ValidCreateCandidate() =>
        new(
            "CAN-01", "ANON-01", "Ada", "Lovelace", "ada@example.com", "+90-555",
            CandidateSource.Referral, null);

    private static UpdateCandidateRequest ValidUpdateCandidate() =>
        new(
            "CAN-01", "Ada", "Lovelace", "ada@example.com", "+90-555",
            CandidateSource.Referral, null);

    private static string BoundaryEmail(int length)
    {
        var domain = string.Join(
            ".",
            new string('d', 63),
            new string('d', 63),
            new string('d', 63),
            new string('d', 63));
        var localPart = new string('a', length - domain.Length - 1);
        return $"{localPart}@{domain}";
    }
}
