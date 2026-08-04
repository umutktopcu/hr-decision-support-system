using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.Employees.Dtos;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

public class EmployeeServiceTests
{
    [Fact]
    public async Task ListAsync_EmptyDatabase_ReturnsSuccessfulEmptyList()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await CreateService(context).ListAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListAsync_OrdersByEmployeeCode()
    {
        await using var context = TestDatabase.CreateContext();
        AddEmployee(context, "EMP-20", "ANON-20");
        AddEmployee(context, "EMP-01", "ANON-01");
        await context.SaveChangesAsync();

        var result = await CreateService(context).ListAsync();

        Assert.Equal(["EMP-01", "EMP-20"], result.Value.Select(item => item.EmployeeCode));
    }

    [Fact]
    public async Task ListAsync_UsesNewestOpenAssignmentAndMapsDepartmentAndPosition()
    {
        await using var context = TestDatabase.CreateContext();
        var employee = AddEmployee(context, "EMP-01", "ANON-01");
        var oldDepartment = TestDatabase.Department();
        var oldPosition = TestDatabase.Position();
        var currentDepartment = TestDatabase.Department();
        currentDepartment.Name = "People Operations";
        var currentPosition = TestDatabase.Position();
        currentPosition.Name = "HR Business Partner";
        context.AddRange(oldDepartment, oldPosition, currentDepartment, currentPosition);
        context.EmployeeAssignments.AddRange(
            Assignment(employee, oldDepartment, oldPosition, new DateOnly(2025, 1, 1)),
            Assignment(employee, currentDepartment, currentPosition, new DateOnly(2026, 1, 1)));
        await context.SaveChangesAsync();

        var item = Assert.Single((await CreateService(context).ListAsync()).Value);

        Assert.Equal(currentDepartment.Id, item.CurrentDepartmentId);
        Assert.Equal("People Operations", item.CurrentDepartmentName);
        Assert.Equal(currentPosition.Id, item.CurrentPositionId);
        Assert.Equal("HR Business Partner", item.CurrentPositionName);
    }

    [Fact]
    public async Task GetByIdAsync_MissingEmployee_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await CreateService(context).GetByIdAsync(Guid.NewGuid());

        AssertFailure(result, "employee_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task GetByIdAsync_OrdersAssignmentsByStartDateDescending()
    {
        await using var context = TestDatabase.CreateContext();
        var employee = AddEmployee(context, "EMP-01", "ANON-01");
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        context.AddRange(department, position);
        context.EmployeeAssignments.AddRange(
            Assignment(employee, department, position, new DateOnly(2024, 1, 1)),
            Assignment(employee, department, position, new DateOnly(2026, 1, 1)),
            Assignment(employee, department, position, new DateOnly(2025, 1, 1)));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetByIdAsync(employee.Id);

        Assert.Equal(
            [new DateOnly(2026, 1, 1), new DateOnly(2025, 1, 1), new DateOnly(2024, 1, 1)],
            result.Value.Assignments.Select(item => item.StartDate));
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesPersonEmployeeAndAssignment()
    {
        await using var context = TestDatabase.CreateContext();
        var (department, position) = AddReferences(context);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(
            CreateRequest(department.Id, position.Id) with
            {
                EmployeeCode = " EMP-01 ",
                AnonymousCode = " ANON-01 "
            });

        Assert.True(result.IsSuccess);
        Assert.Equal("EMP-01", result.Value.EmployeeCode);
        Assert.Equal("ANON-01", result.Value.AnonymousCode);
        Assert.Equal(1, await context.People.CountAsync());
        Assert.Equal(1, await context.Employees.CountAsync());
        Assert.Equal(1, await context.EmployeeAssignments.CountAsync());
        Assert.Equal(result.Value.PersonId, (await context.Employees.SingleAsync()).PersonId);
    }

    [Fact]
    public async Task CreateAsync_NullAssignmentStartDate_UsesHireDate()
    {
        await using var context = TestDatabase.CreateContext();
        var (department, position) = AddReferences(context);
        await context.SaveChangesAsync();
        var request = CreateRequest(department.Id, position.Id) with
        {
            HireDate = new DateOnly(2023, 7, 10),
            InitialAssignmentStartDate = null
        };

        await CreateService(context).CreateAsync(request);

        Assert.Equal(request.HireDate, (await context.EmployeeAssignments.SingleAsync()).StartDate);
    }

    [Fact]
    public async Task CreateAsync_DuplicateEmployeeCode_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        AddEmployee(context, "EMP-01", "EXISTING");
        var (department, position) = AddReferences(context);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest(department.Id, position.Id));

        AssertFailure(result, "employee_code_conflict", ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateAsync_MissingDepartment_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var position = TestDatabase.Position();
        context.Positions.Add(position);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(
            CreateRequest(Guid.NewGuid(), position.Id));

        AssertFailure(result, "department_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task CreateAsync_InactiveDepartment_ReturnsFailure()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department(false);
        var position = TestDatabase.Position();
        context.AddRange(department, position);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest(department.Id, position.Id));

        AssertFailure(result, "department_inactive", ErrorType.Failure);
    }

    [Fact]
    public async Task CreateAsync_MissingPosition_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        context.Departments.Add(department);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(
            CreateRequest(department.Id, Guid.NewGuid()));

        AssertFailure(result, "position_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task CreateAsync_InactivePosition_ReturnsFailure()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position(false);
        context.AddRange(department, position);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest(department.Id, position.Id));

        AssertFailure(result, "position_inactive", ErrorType.Failure);
    }

    [Fact]
    public async Task CreateAsync_ExistingCandidatePerson_ReusesPerson()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("ANON-01");
        context.People.Add(person);
        context.Candidates.Add(new Candidate
        {
            Id = Guid.NewGuid(), PersonId = person.Id, Person = person,
            CandidateCode = "CAN-01", CandidateSource = CandidateSource.Referral
        });
        var (department, position) = AddReferences(context);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest(department.Id, position.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(person.Id, result.Value.PersonId);
        Assert.Equal(1, await context.People.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ExistingRolelessPerson_ReusesPersonWithoutCreatingSecondPerson()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("ANON-01");
        context.People.Add(person);
        var (department, position) = AddReferences(context);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest(department.Id, position.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(person.Id, result.Value.PersonId);
        Assert.Equal(1, await context.People.CountAsync());
        Assert.Equal(person.Id, (await context.Employees.SingleAsync()).PersonId);
    }

    [Fact]
    public async Task CreateAsync_ExistingPersonWithNullFields_CompletesMissingPersonData()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person(
            "ANON-01",
            firstName: null,
            lastName: null,
            email: null,
            phoneNumber: null);
        context.People.Add(person);
        var (department, position) = AddReferences(context);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest(department.Id, position.Id));

        Assert.True(result.IsSuccess);
        var persisted = await context.People.SingleAsync();
        Assert.Equal("Ada", persisted.FirstName);
        Assert.Equal("Lovelace", persisted.LastName);
        Assert.Equal("ada@example.com", persisted.Email);
        Assert.Equal("+90-555-000-0000", persisted.PhoneNumber);
        Assert.NotNull(persisted.UpdatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_ExistingEmailDiffersOnlyByCase_DoesNotReturnPersonDataConflict()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("ANON-01", email: "ADA@EXAMPLE.COM");
        context.People.Add(person);
        var (department, position) = AddReferences(context);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest(department.Id, position.Id));

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Errors, error => error.Code == "person_data_conflict");
        Assert.Equal(person.Id, result.Value.PersonId);
        Assert.Equal(1, await context.People.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_PersonAlreadyEmployee_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        AddEmployee(context, "OTHER", "ANON-01");
        var (department, position) = AddReferences(context);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest(department.Id, position.Id));

        AssertFailure(result, "person_employee_role_conflict", ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateAsync_ConflictingPersonData_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        context.People.Add(TestDatabase.Person("ANON-01", firstName: "Grace"));
        var (department, position) = AddReferences(context);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest(department.Id, position.Id));

        AssertFailure(result, "person_data_conflict", ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateAsync_TerminatedWithoutTerminationDate_ReturnsValidationFailure()
    {
        await using var context = TestDatabase.CreateContext();
        var request = CreateRequest(Guid.NewGuid(), Guid.NewGuid()) with
        {
            EmploymentStatus = EmploymentStatus.Terminated,
            TerminationDate = null
        };

        var result = await CreateService(context).CreateAsync(request);

        AssertFailure(result, "termination_date_required", ErrorType.Validation);
    }

    [Fact]
    public async Task CreateAsync_TerminationBeforeHireDate_ReturnsValidationFailure()
    {
        await using var context = TestDatabase.CreateContext();
        var request = CreateRequest(Guid.NewGuid(), Guid.NewGuid()) with
        {
            EmploymentStatus = EmploymentStatus.Terminated,
            HireDate = new DateOnly(2025, 1, 1),
            TerminationDate = new DateOnly(2024, 12, 31)
        };

        var result = await CreateService(context).CreateAsync(request);

        Assert.Contains(result.Errors, error => error.Code == "termination_date_before_hire_date");
    }

    [Fact]
    public async Task CreateAsync_NonTerminatedStatusWithTerminationDate_ReturnsValidationFailure()
    {
        await using var context = TestDatabase.CreateContext();
        var request = CreateRequest(Guid.NewGuid(), Guid.NewGuid()) with
        {
            EmploymentStatus = EmploymentStatus.Active,
            TerminationDate = new DateOnly(2026, 1, 1)
        };

        var result = await CreateService(context).CreateAsync(request);

        AssertFailure(result, "termination_date_not_allowed", ErrorType.Validation);
    }

    [Fact]
    public async Task UpdateAsync_MissingEmployee_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await CreateService(context).UpdateAsync(Guid.NewGuid(), UpdateRequest());

        AssertFailure(result, "employee_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateEmployeeCode_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        var employee = AddEmployee(context, "EMP-01", "ANON-01");
        AddEmployee(context, "EMP-02", "ANON-02");
        await context.SaveChangesAsync();

        var result = await CreateService(context).UpdateAsync(
            employee.Id,
            UpdateRequest() with { EmployeeCode = "EMP-02" });

        AssertFailure(result, "employee_code_conflict", ErrorType.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesEmployeeAndPersonFields()
    {
        await using var context = TestDatabase.CreateContext();
        var employee = AddEmployee(context, "EMP-01", "ANON-01");
        await context.SaveChangesAsync();
        var request = UpdateRequest() with
        {
            EmployeeCode = " EMP-NEW ",
            FirstName = "Katherine",
            Email = "katherine@example.com",
            EmploymentStatus = EmploymentStatus.Terminated,
            TerminationDate = new DateOnly(2026, 1, 1)
        };

        var result = await CreateService(context).UpdateAsync(employee.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("EMP-NEW", result.Value.EmployeeCode);
        Assert.Equal("Katherine", result.Value.FirstName);
        Assert.Equal("katherine@example.com", result.Value.Email);
        Assert.Equal(EmploymentStatus.Terminated, result.Value.EmploymentStatus);
        Assert.NotNull((await context.People.FindAsync(employee.PersonId))!.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeAssignments()
    {
        await using var context = TestDatabase.CreateContext();
        var employee = AddEmployee(context, "EMP-01", "ANON-01");
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var assignment = Assignment(employee, department, position, new DateOnly(2024, 1, 1));
        context.AddRange(department, position, assignment);
        await context.SaveChangesAsync();
        var original = (assignment.DepartmentId, assignment.PositionId, assignment.StartDate, assignment.EndDate);

        await CreateService(context).UpdateAsync(employee.Id, UpdateRequest());

        var persisted = await context.EmployeeAssignments.SingleAsync();
        Assert.Equal(original, (persisted.DepartmentId, persisted.PositionId, persisted.StartDate, persisted.EndDate));
    }

    private static EmployeeService CreateService(HrDecisionSupportDbContext context) =>
        new(context, new CreateEmployeeRequestValidator(), new UpdateEmployeeRequestValidator());

    private static Employee AddEmployee(
        HrDecisionSupportDbContext context,
        string employeeCode,
        string anonymousCode)
    {
        var person = TestDatabase.Person(anonymousCode);
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            Person = person,
            EmployeeCode = employeeCode,
            HireDate = new DateOnly(2024, 1, 1),
            EmploymentStatus = EmploymentStatus.Active
        };
        context.People.Add(person);
        context.Employees.Add(employee);
        return employee;
    }

    private static EmployeeAssignment Assignment(
        Employee employee,
        Department department,
        Position position,
        DateOnly startDate) =>
        new()
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Employee = employee,
            DepartmentId = department.Id,
            Department = department,
            PositionId = position.Id,
            Position = position,
            StartDate = startDate
        };

    private static (Department Department, Position Position) AddReferences(
        HrDecisionSupportDbContext context)
    {
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        context.AddRange(department, position);
        return (department, position);
    }

    private static CreateEmployeeRequest CreateRequest(Guid departmentId, Guid positionId) =>
        new(
            "EMP-01",
            "ANON-01",
            "Ada",
            "Lovelace",
            "ada@example.com",
            "+90-555-000-0000",
            new DateOnly(2024, 1, 1),
            null,
            EmploymentStatus.Active,
            departmentId,
            positionId,
            null);

    private static UpdateEmployeeRequest UpdateRequest() =>
        new(
            "EMP-01",
            "Ada",
            "Lovelace",
            "ada@example.com",
            "+90-555-000-0000",
            new DateOnly(2024, 1, 1),
            null,
            EmploymentStatus.Active);

    private static void AssertFailure<T>(Result<T> result, string code, ErrorType type)
    {
        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == code && error.Type == type);
    }
}
