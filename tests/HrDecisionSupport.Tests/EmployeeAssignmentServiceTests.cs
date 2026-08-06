using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Employees.Assignments;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

public class EmployeeAssignmentServiceTests
{
    [Fact]
    public async Task ListByEmployeeAsync_ExistingEmployeeWithoutAssignments_ReturnsEmptyList()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.ListByEmployeeAsync(fixture.Employee.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListByEmployeeAsync_MissingEmployee_ReturnsNotFound()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.ListByEmployeeAsync(Guid.NewGuid());

        AssertError(result, "employee_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task ListByEmployeeAsync_EmptyEmployeeId_ReturnsValidationError()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.ListByEmployeeAsync(Guid.Empty);

        var error = Assert.IsType<ValidationError>(Assert.Single(result.Errors));
        Assert.Equal("employee_id_required", error.Code);
        Assert.Equal("EmployeeId", error.PropertyName);
    }

    [Fact]
    public async Task ListByEmployeeAsync_OrdersCurrentFirstThenStartDescendingAndMapsReferences()
    {
        await using var fixture = await Fixture.CreateAsync();
        var oldest = fixture.Assignment(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31));
        var newestClosed = fixture.Assignment(new DateOnly(2025, 1, 1), new DateOnly(2025, 3, 31));
        var current = fixture.Assignment(new DateOnly(2024, 4, 1), null);
        fixture.Context.EmployeeAssignments.AddRange(oldest, newestClosed, current);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ListByEmployeeAsync(fixture.Employee.Id);

        Assert.Equal([current.Id, newestClosed.Id, oldest.Id], result.Value.Select(item => item.Id));
        var dto = result.Value[0];
        Assert.Equal(fixture.Employee.EmployeeCode, dto.EmployeeCode);
        Assert.Equal(fixture.Department.Code, dto.DepartmentCode);
        Assert.Equal(fixture.Department.Name, dto.DepartmentName);
        Assert.Equal(fixture.Position.Code, dto.PositionCode);
        Assert.Equal(fixture.Position.Name, dto.PositionName);
        Assert.True(dto.IsCurrent);
    }

    [Fact]
    public async Task ListByEmployeeAsync_UsesIdAsDeterministicTieBreaker()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = fixture.Assignment(new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 1));
        var second = fixture.Assignment(new DateOnly(2024, 1, 1), new DateOnly(2024, 2, 1));
        first.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        second.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        fixture.Context.EmployeeAssignments.AddRange(second, first);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ListByEmployeeAsync(fixture.Employee.Id);

        Assert.Equal([first.Id, second.Id], result.Value.Select(item => item.Id));
    }

    [Fact]
    public async Task GetByIdAsync_MissingAssignment_ReturnsNotFound()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.GetByIdAsync(Guid.NewGuid());

        AssertError(result, "employee_assignment_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task GetByIdAsync_OpenAssignment_ReturnsCurrentProjectedDto()
    {
        await using var fixture = await Fixture.CreateAsync();
        var assignment = fixture.Assignment(new DateOnly(2024, 1, 1), null);
        fixture.Context.EmployeeAssignments.Add(assignment);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.GetByIdAsync(assignment.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsCurrent);
        Assert.Null(result.Value.EndDate);
        Assert.Equal(fixture.Department.Id, result.Value.DepartmentId);
        Assert.Equal(fixture.Position.Id, result.Value.PositionId);
    }

    [Fact]
    public async Task CreateAsync_ValidClosedAssignment_CreatesAndReturnsDto()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.CreateRequest(
            new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31));

        var result = await fixture.Service.CreateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsCurrent);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal(1, await fixture.Context.EmployeeAssignments.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ValidOpenAssignment_CreatesCurrentAssignment()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.CreateAsync(fixture.CreateRequest(new DateOnly(2024, 1, 1), null));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsCurrent);
    }

    [Fact]
    public async Task CreateAsync_MissingEmployee_ReturnsNotFound()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.CreateRequest(new DateOnly(2024, 1, 1), null) with
        {
            EmployeeId = Guid.NewGuid()
        };

        var result = await fixture.Service.CreateAsync(request);

        AssertError(result, "employee_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task CreateAsync_MissingOrInactiveDepartment_ReturnsExistingReferenceErrors()
    {
        await using var fixture = await Fixture.CreateAsync();
        var missing = fixture.CreateRequest(new DateOnly(2024, 1, 1), null) with
        {
            DepartmentId = Guid.NewGuid()
        };

        AssertError(await fixture.Service.CreateAsync(missing), "department_not_found", ErrorType.NotFound);
        fixture.Department.IsActive = false;
        await fixture.Context.SaveChangesAsync();
        AssertError(
            await fixture.Service.CreateAsync(fixture.CreateRequest(new DateOnly(2024, 1, 1), null)),
            "department_inactive",
            ErrorType.Failure);
    }

    [Fact]
    public async Task CreateAsync_MissingOrInactivePosition_ReturnsExistingReferenceErrors()
    {
        await using var fixture = await Fixture.CreateAsync();
        var missing = fixture.CreateRequest(new DateOnly(2024, 1, 1), null) with
        {
            PositionId = Guid.NewGuid()
        };

        AssertError(await fixture.Service.CreateAsync(missing), "position_not_found", ErrorType.NotFound);
        fixture.Position.IsActive = false;
        await fixture.Context.SaveChangesAsync();
        AssertError(
            await fixture.Service.CreateAsync(fixture.CreateRequest(new DateOnly(2024, 1, 1), null)),
            "position_inactive",
            ErrorType.Failure);
    }

    [Fact]
    public async Task CreateAsync_EmptyIdsAndInvalidRange_ReturnsAllValidationErrors()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = new CreateEmployeeAssignmentRequest(
            Guid.Empty,
            Guid.Empty,
            Guid.Empty,
            new DateOnly(2024, 2, 1),
            new DateOnly(2024, 1, 31));

        var result = await fixture.Service.CreateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(4, result.Errors.Count);
        Assert.All(result.Errors, error => Assert.Equal(ErrorType.Validation, error.Type));
    }

    [Fact]
    public async Task CreateAsync_StartBeforeHire_ReturnsFailure()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(new DateOnly(2023, 12, 31), new DateOnly(2024, 1, 2)));

        AssertError(result, "employee_assignment_before_hire_date", ErrorType.Failure);
    }

    [Fact]
    public async Task CreateAsync_AssignmentAfterTermination_ReturnsFailure()
    {
        await using var fixture = await Fixture.CreateAsync(
            EmploymentStatus.Terminated, new DateOnly(2024, 6, 30));

        var result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(new DateOnly(2024, 6, 1), new DateOnly(2024, 7, 1)));

        AssertError(result, "employee_assignment_after_termination_date", ErrorType.Failure);
    }

    [Fact]
    public async Task CreateAsync_TerminatedEmployeeCannotReceiveOpenAssignment()
    {
        await using var fixture = await Fixture.CreateAsync(
            EmploymentStatus.Terminated, new DateOnly(2024, 6, 30));

        var result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(new DateOnly(2024, 6, 1), null));

        AssertError(result, "employee_assignment_after_termination_date", ErrorType.Failure);
    }

    [Theory]
    [InlineData(EmploymentStatus.Terminated)]
    [InlineData(EmploymentStatus.Active)]
    public async Task CreateAsync_InconsistentEmployeeStateRejectsOpenAssignment(EmploymentStatus status)
    {
        DateOnly? terminationDate = status == EmploymentStatus.Active
            ? new DateOnly(2024, 6, 30)
            : null;
        await using var fixture = await Fixture.CreateAsync(status, terminationDate);

        var result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(new DateOnly(2024, 1, 1), null));

        AssertError(result, "employee_assignment_employee_state_conflict", ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateAsync_SecondOpenAssignment_ReturnsOpenConflict()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.EmployeeAssignments.Add(fixture.Assignment(new DateOnly(2024, 1, 1), null));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(new DateOnly(2025, 1, 1), null));

        AssertError(result, "employee_assignment_open_conflict", ErrorType.Conflict);
    }

    [Theory]
    [InlineData("2024-01-31", "2024-02-10")]
    [InlineData("2024-01-15", "2024-01-31")]
    public async Task CreateAsync_InclusiveOverlap_ReturnsConflict(string start, string end)
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.EmployeeAssignments.Add(fixture.Assignment(
            new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31)));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(DateOnly.Parse(start), DateOnly.Parse(end)));

        AssertError(result, "employee_assignment_overlap", ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateAsync_StartOnDayAfterExistingEnd_DoesNotOverlap()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.EmployeeAssignments.Add(fixture.Assignment(
            new DateOnly(2024, 1, 1), new DateOnly(2024, 1, 31)));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CreateAsync(
            fixture.CreateRequest(new DateOnly(2024, 2, 1), new DateOnly(2024, 2, 28)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateAsync_MissingAssignment_ReturnsNotFound()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.UpdateAsync(
            Guid.NewGuid(), fixture.UpdateRequest(new DateOnly(2024, 1, 1), null));

        AssertError(result, "employee_assignment_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_MissingDepartmentOrPosition_ReturnsNotFound()
    {
        await using var fixture = await Fixture.CreateAsync();
        var assignment = fixture.Assignment(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 1));
        fixture.Context.EmployeeAssignments.Add(assignment);
        await fixture.Context.SaveChangesAsync();

        var missingDepartment = fixture.UpdateRequest(assignment.StartDate!.Value, assignment.EndDate) with
        {
            DepartmentId = Guid.NewGuid()
        };
        AssertError(await fixture.Service.UpdateAsync(assignment.Id, missingDepartment),
            "department_not_found", ErrorType.NotFound);
        var missingPosition = fixture.UpdateRequest(assignment.StartDate!.Value, assignment.EndDate) with
        {
            PositionId = Guid.NewGuid()
        };
        AssertError(await fixture.Service.UpdateAsync(assignment.Id, missingPosition),
            "position_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_ValidCorrectionUpdatesAllowedFieldsAndKeepsEmployee()
    {
        await using var fixture = await Fixture.CreateAsync();
        var newDepartment = TestDatabase.Department();
        var newPosition = TestDatabase.Position();
        var assignment = fixture.Assignment(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 1));
        fixture.Context.AddRange(newDepartment, newPosition, assignment);
        await fixture.Context.SaveChangesAsync();
        var request = new UpdateEmployeeAssignmentRequest(
            newDepartment.Id,
            newPosition.Id,
            new DateOnly(2024, 1, 2),
            new DateOnly(2024, 3, 2));

        var result = await fixture.Service.UpdateAsync(assignment.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal(fixture.Employee.Id, result.Value.EmployeeId);
        Assert.Equal(newDepartment.Id, result.Value.DepartmentId);
        Assert.Equal(newPosition.Id, result.Value.PositionId);
        Assert.Equal(request.StartDate, result.Value.StartDate);
        Assert.Equal(request.EndDate, result.Value.EndDate);
    }

    [Fact]
    public async Task UpdateAsync_OwnRecordIsExcludedFromOverlapCheck()
    {
        await using var fixture = await Fixture.CreateAsync();
        var assignment = fixture.Assignment(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 1));
        fixture.Context.EmployeeAssignments.Add(assignment);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.UpdateAsync(
            assignment.Id, fixture.UpdateRequest(assignment.StartDate!.Value, assignment.EndDate));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateAsync_OverlapWithAnotherAssignment_ReturnsConflict()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = fixture.Assignment(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31));
        var second = fixture.Assignment(new DateOnly(2024, 4, 1), new DateOnly(2024, 6, 30));
        fixture.Context.EmployeeAssignments.AddRange(first, second);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.UpdateAsync(second.Id,
            fixture.UpdateRequest(new DateOnly(2024, 3, 31), second.EndDate));

        AssertError(result, "employee_assignment_overlap", ErrorType.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_CannotCreateSecondOpenAssignment()
    {
        await using var fixture = await Fixture.CreateAsync();
        var closed = fixture.Assignment(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31));
        var current = fixture.Assignment(new DateOnly(2024, 4, 1), null);
        fixture.Context.EmployeeAssignments.AddRange(closed, current);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.UpdateAsync(closed.Id,
            fixture.UpdateRequest(closed.StartDate!.Value, null));

        AssertError(result, "employee_assignment_open_conflict", ErrorType.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_ClosedToOpenAndOpenToClosedTransitionsWork()
    {
        await using var fixture = await Fixture.CreateAsync();
        var assignment = fixture.Assignment(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31));
        fixture.Context.EmployeeAssignments.Add(assignment);
        await fixture.Context.SaveChangesAsync();

        var opened = await fixture.Service.UpdateAsync(
            assignment.Id, fixture.UpdateRequest(assignment.StartDate!.Value, null));
        var closed = await fixture.Service.UpdateAsync(
            assignment.Id,
            fixture.UpdateRequest(assignment.StartDate!.Value, new DateOnly(2024, 4, 30)));

        Assert.True(opened.Value.IsCurrent);
        Assert.False(closed.Value.IsCurrent);
    }

    [Fact]
    public async Task UpdateAsync_EnforcesHireAndTerminationDates()
    {
        await using var fixture = await Fixture.CreateAsync(
            EmploymentStatus.Terminated, new DateOnly(2024, 6, 30));
        var assignment = fixture.Assignment(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31));
        fixture.Context.EmployeeAssignments.Add(assignment);
        await fixture.Context.SaveChangesAsync();

        AssertError(await fixture.Service.UpdateAsync(assignment.Id,
                fixture.UpdateRequest(new DateOnly(2023, 12, 31), assignment.EndDate)),
            "employee_assignment_before_hire_date", ErrorType.Failure);
        AssertError(await fixture.Service.UpdateAsync(assignment.Id,
                fixture.UpdateRequest(assignment.StartDate!.Value, new DateOnly(2024, 7, 1))),
            "employee_assignment_after_termination_date", ErrorType.Failure);
    }

    [Theory]
    [InlineData(EmploymentStatus.Terminated, false)]
    [InlineData(EmploymentStatus.Active, true)]
    [InlineData(EmploymentStatus.OnLeave, true)]
    public async Task UpdateAsync_InconsistentEmployeeStateRejectsOpenAssignmentWithoutChangingRecord(
        EmploymentStatus status,
        bool hasTerminationDate)
    {
        DateOnly? terminationDate = hasTerminationDate ? new DateOnly(2024, 6, 30) : null;
        await using var fixture = await Fixture.CreateAsync(status, terminationDate);
        var assignment = fixture.Assignment(
            new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31));
        fixture.Context.EmployeeAssignments.Add(assignment);
        await fixture.Context.SaveChangesAsync();
        var originalEmployeeId = assignment.EmployeeId;
        var originalDepartmentId = assignment.DepartmentId;
        var originalPositionId = assignment.PositionId;
        var originalStartDate = assignment.StartDate;
        var originalEndDate = assignment.EndDate;

        var result = await fixture.Service.UpdateAsync(
            assignment.Id, fixture.UpdateRequest(assignment.StartDate!.Value, null));

        AssertError(result, "employee_assignment_employee_state_conflict", ErrorType.Conflict);
        var stored = await fixture.Context.EmployeeAssignments.AsNoTracking()
            .SingleAsync(item => item.Id == assignment.Id);
        Assert.Equal(originalEmployeeId, stored.EmployeeId);
        Assert.Equal(originalDepartmentId, stored.DepartmentId);
        Assert.Equal(originalPositionId, stored.PositionId);
        Assert.Equal(originalStartDate, stored.StartDate);
        Assert.Equal(originalEndDate, stored.EndDate);
    }

    [Fact]
    public async Task ChangeCurrentAsync_ValidTransferClosesOldAndCreatesOneOpenAssignment()
    {
        await using var fixture = await Fixture.CreateAsync();
        var current = fixture.Assignment(new DateOnly(2024, 1, 1), null);
        var newDepartment = TestDatabase.Department();
        fixture.Context.AddRange(current, newDepartment);
        await fixture.Context.SaveChangesAsync();
        var request = new ChangeCurrentEmployeeAssignmentRequest(
            fixture.Employee.Id,
            newDepartment.Id,
            fixture.Position.Id,
            new DateOnly(2024, 4, 1));

        var result = await fixture.Service.ChangeCurrentAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.EndDate);
        Assert.Equal(new DateOnly(2024, 3, 31),
            (await fixture.Context.EmployeeAssignments.FindAsync(current.Id))!.EndDate);
        Assert.Equal(1, await fixture.Context.EmployeeAssignments.CountAsync(item => item.EndDate == null));
    }

    [Fact]
    public async Task ChangeCurrentAsync_NoCurrentAssignment_ReturnsNotFound()
    {
        await using var fixture = await Fixture.CreateAsync();
        var newDepartment = TestDatabase.Department();
        fixture.Context.Departments.Add(newDepartment);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ChangeCurrentAsync(new(
            fixture.Employee.Id, newDepartment.Id, fixture.Position.Id, new DateOnly(2024, 4, 1)));

        AssertError(result, "current_employee_assignment_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task ChangeCurrentAsync_MultipleCurrentAssignments_ReturnsStateConflict()
    {
        await using var fixture = await Fixture.CreateAsync();
        var newDepartment = TestDatabase.Department();
        fixture.Context.AddRange(
            newDepartment,
            fixture.Assignment(new DateOnly(2024, 1, 1), null),
            fixture.Assignment(new DateOnly(2024, 2, 1), null));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ChangeCurrentAsync(new(
            fixture.Employee.Id, newDepartment.Id, fixture.Position.Id, new DateOnly(2024, 4, 1)));

        AssertError(result, "employee_assignment_state_conflict", ErrorType.Conflict);
    }

    [Theory]
    [InlineData("2024-01-01")]
    [InlineData("2023-12-31")]
    public async Task ChangeCurrentAsync_NewStartNotAfterCurrent_ReturnsFailure(string date)
    {
        await using var fixture = await Fixture.CreateAsync();
        var newDepartment = TestDatabase.Department();
        fixture.Context.AddRange(newDepartment, fixture.Assignment(new DateOnly(2024, 1, 1), null));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ChangeCurrentAsync(new(
            fixture.Employee.Id, newDepartment.Id, fixture.Position.Id, DateOnly.Parse(date)));

        AssertError(result, "employee_assignment_change_date_invalid", ErrorType.Failure);
    }

    [Fact]
    public async Task ChangeCurrentAsync_BeforeHireDate_ReturnsFailure()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Employee.HireDate = new DateOnly(2024, 2, 1);
        var newDepartment = TestDatabase.Department();
        fixture.Context.AddRange(newDepartment, fixture.Assignment(new DateOnly(2024, 1, 1), null));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ChangeCurrentAsync(new(
            fixture.Employee.Id, newDepartment.Id, fixture.Position.Id, new DateOnly(2024, 1, 15)));

        AssertError(result, "employee_assignment_before_hire_date", ErrorType.Failure);
    }

    [Fact]
    public async Task ChangeCurrentAsync_AfterTerminationDate_ReturnsFailure()
    {
        await using var fixture = await Fixture.CreateAsync(
            EmploymentStatus.Terminated, new DateOnly(2024, 6, 30));
        var newDepartment = TestDatabase.Department();
        fixture.Context.AddRange(newDepartment, fixture.Assignment(new DateOnly(2024, 1, 1), null));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ChangeCurrentAsync(new(
            fixture.Employee.Id, newDepartment.Id, fixture.Position.Id, new DateOnly(2024, 7, 1)));

        AssertError(result, "employee_assignment_after_termination_date", ErrorType.Failure);
    }

    [Theory]
    [InlineData(EmploymentStatus.Terminated, false)]
    [InlineData(EmploymentStatus.Active, true)]
    [InlineData(EmploymentStatus.OnLeave, true)]
    public async Task ChangeCurrentAsync_InconsistentEmployeeStateDoesNotCloseOrCreateAssignments(
        EmploymentStatus status,
        bool hasTerminationDate)
    {
        DateOnly? terminationDate = hasTerminationDate ? new DateOnly(2024, 6, 30) : null;
        await using var fixture = await Fixture.CreateAsync(status, terminationDate);
        var current = fixture.Assignment(new DateOnly(2024, 1, 1), null);
        var newDepartment = TestDatabase.Department();
        fixture.Context.AddRange(current, newDepartment);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ChangeCurrentAsync(new(
            fixture.Employee.Id,
            newDepartment.Id,
            fixture.Position.Id,
            new DateOnly(2024, 4, 1)));

        AssertError(result, "employee_assignment_employee_state_conflict", ErrorType.Conflict);
        var stored = await fixture.Context.EmployeeAssignments.AsNoTracking()
            .SingleAsync(item => item.Id == current.Id);
        Assert.Null(stored.EndDate);
        Assert.Equal(1, await fixture.Context.EmployeeAssignments.CountAsync());
        Assert.Equal(1, await fixture.Context.EmployeeAssignments.CountAsync(item => item.EndDate == null));
    }

    [Fact]
    public async Task ChangeCurrentAsync_SameDepartmentAndPosition_ReturnsNoChangeConflict()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Context.Add(fixture.Assignment(new DateOnly(2024, 1, 1), null));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ChangeCurrentAsync(new(
            fixture.Employee.Id,
            fixture.Department.Id,
            fixture.Position.Id,
            new DateOnly(2024, 4, 1)));

        AssertError(result, "employee_assignment_no_change", ErrorType.Conflict);
    }

    [Fact]
    public async Task ChangeCurrentAsync_SameDepartmentDifferentPosition_IsValid()
    {
        await using var fixture = await Fixture.CreateAsync();
        var newPosition = TestDatabase.Position();
        fixture.Context.AddRange(newPosition, fixture.Assignment(new DateOnly(2024, 1, 1), null));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ChangeCurrentAsync(new(
            fixture.Employee.Id, fixture.Department.Id, newPosition.Id, new DateOnly(2024, 4, 1)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ChangeCurrentAsync_DifferentDepartmentSamePosition_IsValid()
    {
        await using var fixture = await Fixture.CreateAsync();
        var newDepartment = TestDatabase.Department();
        fixture.Context.AddRange(newDepartment, fixture.Assignment(new DateOnly(2024, 1, 1), null));
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ChangeCurrentAsync(new(
            fixture.Employee.Id, newDepartment.Id, fixture.Position.Id, new DateOnly(2024, 4, 1)));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ChangeCurrentAsync_OverlapWithHistoricalRecord_ReturnsConflict()
    {
        await using var fixture = await Fixture.CreateAsync();
        var newDepartment = TestDatabase.Department();
        var current = fixture.Assignment(new DateOnly(2024, 1, 1), null);
        var futureHistory = fixture.Assignment(new DateOnly(2024, 5, 1), new DateOnly(2024, 5, 31));
        fixture.Context.AddRange(newDepartment, current, futureHistory);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.ChangeCurrentAsync(new(
            fixture.Employee.Id, newDepartment.Id, fixture.Position.Id, new DateOnly(2024, 4, 1)));

        AssertError(result, "employee_assignment_overlap", ErrorType.Conflict);
        Assert.Null(current.EndDate);
    }

    [Fact]
    public async Task ChangeCurrentAsync_DayAfterMinValue_ClosesAtMinValueWithoutOverflow()
    {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Employee.HireDate = DateOnly.MinValue;
        var newDepartment = TestDatabase.Department();
        var current = fixture.Assignment(DateOnly.MinValue, null);
        fixture.Context.AddRange(newDepartment, current);
        await fixture.Context.SaveChangesAsync();
        var newStartDate = DateOnly.MinValue.AddDays(1);

        var result = await fixture.Service.ChangeCurrentAsync(new(
            fixture.Employee.Id, newDepartment.Id, fixture.Position.Id, newStartDate));

        Assert.True(result.IsSuccess);
        var storedCurrent = await fixture.Context.EmployeeAssignments.AsNoTracking()
            .SingleAsync(item => item.Id == current.Id);
        Assert.Equal(DateOnly.MinValue, storedCurrent.EndDate);
        Assert.Equal(newStartDate, result.Value.StartDate);
        Assert.Null(result.Value.EndDate);
        Assert.Equal(1, await fixture.Context.EmployeeAssignments.CountAsync(item => item.EndDate == null));
    }

    [Fact]
    public async Task CloseAsync_ValidOpenAssignment_OnlyChangesEndDate()
    {
        await using var fixture = await Fixture.CreateAsync();
        var assignment = fixture.Assignment(new DateOnly(2024, 1, 1), null);
        fixture.Context.Add(assignment);
        await fixture.Context.SaveChangesAsync();
        var employeeId = assignment.EmployeeId;
        var departmentId = assignment.DepartmentId;
        var positionId = assignment.PositionId;
        var startDate = assignment.StartDate;

        var result = await fixture.Service.CloseAsync(
            assignment.Id, new(new DateOnly(2024, 3, 31)));

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2024, 3, 31), result.Value.EndDate);
        Assert.Equal(employeeId, result.Value.EmployeeId);
        Assert.Equal(departmentId, result.Value.DepartmentId);
        Assert.Equal(positionId, result.Value.PositionId);
        Assert.Equal(startDate, result.Value.StartDate);
    }

    [Fact]
    public async Task CloseAsync_AlreadyClosed_ReturnsConflict()
    {
        await using var fixture = await Fixture.CreateAsync();
        var assignment = fixture.Assignment(new DateOnly(2024, 1, 1), new DateOnly(2024, 3, 31));
        fixture.Context.Add(assignment);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CloseAsync(assignment.Id, new(new DateOnly(2024, 4, 1)));

        AssertError(result, "employee_assignment_already_closed", ErrorType.Conflict);
    }

    [Fact]
    public async Task CloseAsync_MissingAssignment_ReturnsNotFound()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.Service.CloseAsync(Guid.NewGuid(), new(new DateOnly(2024, 4, 1)));

        AssertError(result, "employee_assignment_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task CloseAsync_EndBeforeStart_ReturnsFailure()
    {
        await using var fixture = await Fixture.CreateAsync();
        var assignment = fixture.Assignment(new DateOnly(2024, 2, 1), null);
        fixture.Context.Add(assignment);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CloseAsync(assignment.Id, new(new DateOnly(2024, 1, 31)));

        AssertError(result, "employee_assignment_end_before_start", ErrorType.Failure);
    }

    [Fact]
    public async Task CloseAsync_EndAfterTermination_ReturnsFailure()
    {
        await using var fixture = await Fixture.CreateAsync(
            EmploymentStatus.Terminated, new DateOnly(2024, 6, 30));
        var assignment = fixture.Assignment(new DateOnly(2024, 1, 1), null);
        fixture.Context.Add(assignment);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CloseAsync(assignment.Id, new(new DateOnly(2024, 7, 1)));

        AssertError(result, "employee_assignment_after_termination_date", ErrorType.Failure);
    }

    [Fact]
    public async Task CloseAsync_OverlapWithAnotherAssignment_ReturnsConflict()
    {
        await using var fixture = await Fixture.CreateAsync();
        var open = fixture.Assignment(new DateOnly(2024, 1, 1), null);
        var other = fixture.Assignment(new DateOnly(2024, 3, 1), new DateOnly(2024, 3, 31));
        fixture.Context.AddRange(open, other);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.CloseAsync(open.Id, new(new DateOnly(2024, 3, 15)));

        AssertError(result, "employee_assignment_overlap", ErrorType.Conflict);
    }

    private static void AssertError(Result result, string code, ErrorType type)
    {
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(code, error.Code);
        Assert.Equal(type, error.Type);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private Fixture(
            HrDecisionSupportDbContext context,
            EmployeeAssignmentService service,
            Employee employee,
            Department department,
            Position position)
        {
            Context = context;
            Service = service;
            Employee = employee;
            Department = department;
            Position = position;
        }

        internal HrDecisionSupportDbContext Context { get; }
        internal EmployeeAssignmentService Service { get; }
        internal Employee Employee { get; }
        internal Department Department { get; }
        internal Position Position { get; }

        internal static async Task<Fixture> CreateAsync(
            EmploymentStatus status = EmploymentStatus.Active,
            DateOnly? terminationDate = null)
        {
            var context = TestDatabase.CreateContext();
            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                PersonId = Guid.NewGuid(),
                EmployeeCode = $"EMP-{Guid.NewGuid():N}"[..20],
                HireDate = new DateOnly(2024, 1, 1),
                TerminationDate = terminationDate,
                EmploymentStatus = status
            };
            var department = TestDatabase.Department();
            var position = TestDatabase.Position();
            context.AddRange(employee, department, position);
            await context.SaveChangesAsync();
            return new(
                context,
                new EmployeeAssignmentService(
                    context,
                    new CreateEmployeeAssignmentRequestValidator(),
                    new UpdateEmployeeAssignmentRequestValidator(),
                    new ChangeCurrentEmployeeAssignmentRequestValidator(),
                    new CloseEmployeeAssignmentRequestValidator()),
                employee,
                department,
                position);
        }

        internal EmployeeAssignment Assignment(DateOnly startDate, DateOnly? endDate) => new()
        {
            Id = Guid.NewGuid(),
            EmployeeId = Employee.Id,
            DepartmentId = Department.Id,
            PositionId = Position.Id,
            StartDate = startDate,
            EndDate = endDate
        };

        internal CreateEmployeeAssignmentRequest CreateRequest(DateOnly startDate, DateOnly? endDate) =>
            new(Employee.Id, Department.Id, Position.Id, startDate, endDate);

        internal UpdateEmployeeAssignmentRequest UpdateRequest(DateOnly startDate, DateOnly? endDate) =>
            new(Department.Id, Position.Id, startDate, endDate);

        public ValueTask DisposeAsync() => Context.DisposeAsync();
    }
}
