using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Employees.Assignments;

namespace HrDecisionSupport.Tests;

public class EmployeeAssignmentValidatorTests
{
    private static readonly Guid EmployeeId = Guid.NewGuid();
    private static readonly Guid DepartmentId = Guid.NewGuid();
    private static readonly Guid PositionId = Guid.NewGuid();
    private static readonly DateOnly StartDate = new(2024, 1, 1);

    [Fact]
    public void CreateValidator_ValidRequest_IsValid()
    {
        var result = new CreateEmployeeAssignmentRequestValidator().Validate(
            new(EmployeeId, DepartmentId, PositionId, StartDate, null));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CreateValidator_CollectsEmptyIdsDefaultDateAndReverseRange()
    {
        var validator = new CreateEmployeeAssignmentRequestValidator();
        var emptyIds = validator.Validate(new(Guid.Empty, Guid.Empty, Guid.Empty, StartDate, null));
        var defaultDate = validator.Validate(new(
            EmployeeId, DepartmentId, PositionId, default, null));
        var reverseRange = validator.Validate(new(
            EmployeeId, DepartmentId, PositionId, StartDate, StartDate.AddDays(-1)));

        AssertValidationErrors(emptyIds,
            ("employee_id_required", "EmployeeId"),
            ("department_id_required", "DepartmentId"),
            ("position_id_required", "PositionId"));
        AssertValidationErrors(defaultDate, ("start_date_required", "StartDate"));
        AssertValidationErrors(reverseRange, ("end_date_before_start_date", "EndDate"));
    }

    [Fact]
    public void UpdateValidator_ValidRequest_IsValid()
    {
        var result = new UpdateEmployeeAssignmentRequestValidator().Validate(
            new(DepartmentId, PositionId, StartDate, StartDate));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void UpdateValidator_CollectsEmptyIdsDefaultDateAndReverseRange()
    {
        var validator = new UpdateEmployeeAssignmentRequestValidator();
        var emptyIds = validator.Validate(new(Guid.Empty, Guid.Empty, StartDate, null));
        var defaultDate = validator.Validate(new(DepartmentId, PositionId, default, null));
        var reverseRange = validator.Validate(new(
            DepartmentId, PositionId, StartDate, StartDate.AddDays(-1)));

        AssertValidationErrors(emptyIds,
            ("department_id_required", "DepartmentId"),
            ("position_id_required", "PositionId"));
        AssertValidationErrors(defaultDate, ("start_date_required", "StartDate"));
        AssertValidationErrors(reverseRange, ("end_date_before_start_date", "EndDate"));
    }

    [Fact]
    public void ChangeCurrentValidator_ValidRequest_IsValid()
    {
        var result = new ChangeCurrentEmployeeAssignmentRequestValidator().Validate(
            new(EmployeeId, DepartmentId, PositionId, StartDate));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ChangeCurrentValidator_CollectsEmptyIdsAndDefaultDate()
    {
        var result = new ChangeCurrentEmployeeAssignmentRequestValidator().Validate(
            new(Guid.Empty, Guid.Empty, Guid.Empty, default));

        AssertValidationErrors(result,
            ("employee_id_required", "EmployeeId"),
            ("department_id_required", "DepartmentId"),
            ("position_id_required", "PositionId"),
            ("new_start_date_required", "NewStartDate"));
    }

    [Fact]
    public void CloseValidator_ValidRequest_IsValid()
    {
        var result = new CloseEmployeeAssignmentRequestValidator().Validate(new(StartDate));

        Assert.True(result.IsValid);
    }

    [Fact]
    public void CloseValidator_DefaultDate_ReturnsCorrectError()
    {
        var result = new CloseEmployeeAssignmentRequestValidator().Validate(new(default));

        AssertValidationErrors(result, ("end_date_required", "EndDate"));
    }

    [Fact]
    public void UpdateRequest_DoesNotExposeEmployeeId()
    {
        var properties = typeof(UpdateEmployeeAssignmentRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToArray();

        Assert.Equal(["DepartmentId", "PositionId", "StartDate", "EndDate"], properties);
        Assert.DoesNotContain("EmployeeId", properties);
    }

    [Fact]
    public void CloseRequest_OnlyExposesEndDate()
    {
        var property = Assert.Single(typeof(CloseEmployeeAssignmentRequest).GetProperties());

        Assert.Equal("EndDate", property.Name);
        Assert.Equal(typeof(DateOnly), property.PropertyType);
    }

    private static void AssertValidationErrors(
        HrDecisionSupport.Application.Common.Validation.ValidationResult result,
        params (string Code, string PropertyName)[] expected)
    {
        Assert.False(result.IsValid);
        Assert.Equal(expected.Length, result.Errors.Count);
        Assert.Equal(expected, result.Errors.Select(error => (error.Code, error.PropertyName)));
        Assert.All(result.Errors, error => Assert.Equal(ErrorType.Validation, error.Type));
    }
}
