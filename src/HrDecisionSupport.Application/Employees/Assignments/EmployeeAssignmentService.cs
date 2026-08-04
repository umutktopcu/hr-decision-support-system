using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Employees.Assignments;

public sealed class EmployeeAssignmentService : IEmployeeAssignmentService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreateEmployeeAssignmentRequest> _createValidator;
    private readonly IValidator<UpdateEmployeeAssignmentRequest> _updateValidator;
    private readonly IValidator<ChangeCurrentEmployeeAssignmentRequest> _changeValidator;
    private readonly IValidator<CloseEmployeeAssignmentRequest> _closeValidator;

    public EmployeeAssignmentService(
        IHrDecisionSupportDbContext dbContext,
        IValidator<CreateEmployeeAssignmentRequest> createValidator,
        IValidator<UpdateEmployeeAssignmentRequest> updateValidator,
        IValidator<ChangeCurrentEmployeeAssignmentRequest> changeValidator,
        IValidator<CloseEmployeeAssignmentRequest> closeValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
        _changeValidator = changeValidator ?? throw new ArgumentNullException(nameof(changeValidator));
        _closeValidator = closeValidator ?? throw new ArgumentNullException(nameof(closeValidator));
    }

    public async Task<Result<IReadOnlyList<EmployeeAssignmentDto>>> ListByEmployeeAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        if (employeeId == Guid.Empty)
        {
            return Result<IReadOnlyList<EmployeeAssignmentDto>>.ValidationFailure(
                new ValidationError(
                    "employee_id_required",
                    "EmployeeId must not be empty.",
                    "EmployeeId"));
        }

        if (!await _dbContext.Employees.AsNoTracking()
                .AnyAsync(employee => employee.Id == employeeId, cancellationToken))
        {
            return Result<IReadOnlyList<EmployeeAssignmentDto>>.Failure(
                UseCaseErrors.EmployeeNotFound(employeeId));
        }

        var assignments = await ProjectAssignments()
            .Where(assignment => assignment.EmployeeId == employeeId)
            .OrderByDescending(assignment => assignment.IsCurrent)
            .ThenByDescending(assignment => assignment.StartDate)
            .ThenBy(assignment => assignment.Id)
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<EmployeeAssignmentDto>>.Success(assignments);
    }

    public async Task<Result<EmployeeAssignmentDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var assignment = await ProjectAssignments()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

        return assignment is null
            ? Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentNotFound)
            : Result<EmployeeAssignmentDto>.Success(assignment);
    }

    public async Task<Result<EmployeeAssignmentDto>> CreateAsync(
        CreateEmployeeAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
            return Result<EmployeeAssignmentDto>.ValidationFailure(validation.Errors);

        var employee = await _dbContext.Employees.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken);
        if (employee is null)
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeNotFound(request.EmployeeId));

        var referenceResult = await ValidateReferencesAsync(
            request.DepartmentId, request.PositionId, cancellationToken);
        if (referenceResult is not null)
            return Result<EmployeeAssignmentDto>.Failure(referenceResult);

        var employeeDateError = ValidateEmployeeDates(
            employee, request.StartDate, request.EndDate, request.EndDate is null);
        if (employeeDateError is not null)
            return Result<EmployeeAssignmentDto>.Failure(employeeDateError);

        if (request.EndDate is null && await HasOtherOpenAssignmentAsync(
                request.EmployeeId, null, cancellationToken))
        {
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentOpenConflict);
        }

        if (await HasOverlapAsync(
                request.EmployeeId,
                request.StartDate,
                request.EndDate,
                null,
                cancellationToken))
        {
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentOverlap);
        }

        var assignment = new EmployeeAssignment
        {
            Id = Guid.NewGuid(),
            EmployeeId = request.EmployeeId,
            DepartmentId = request.DepartmentId,
            PositionId = request.PositionId,
            StartDate = request.StartDate,
            EndDate = request.EndDate
        };

        await _dbContext.EmployeeAssignments.AddAsync(assignment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(assignment.Id, cancellationToken);
    }

    public async Task<Result<EmployeeAssignmentDto>> UpdateAsync(
        Guid id,
        UpdateEmployeeAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var assignment = await _dbContext.EmployeeAssignments
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (assignment is null)
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentNotFound);

        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
            return Result<EmployeeAssignmentDto>.ValidationFailure(validation.Errors);

        var referenceResult = await ValidateReferencesAsync(
            request.DepartmentId, request.PositionId, cancellationToken);
        if (referenceResult is not null)
            return Result<EmployeeAssignmentDto>.Failure(referenceResult);

        var employee = await _dbContext.Employees.AsNoTracking()
            .SingleAsync(item => item.Id == assignment.EmployeeId, cancellationToken);
        var employeeDateError = ValidateEmployeeDates(
            employee, request.StartDate, request.EndDate, request.EndDate is null);
        if (employeeDateError is not null)
            return Result<EmployeeAssignmentDto>.Failure(employeeDateError);

        if (request.EndDate is null && await HasOtherOpenAssignmentAsync(
                assignment.EmployeeId, assignment.Id, cancellationToken))
        {
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentOpenConflict);
        }

        if (await HasOverlapAsync(
                assignment.EmployeeId,
                request.StartDate,
                request.EndDate,
                assignment.Id,
                cancellationToken))
        {
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentOverlap);
        }

        assignment.DepartmentId = request.DepartmentId;
        assignment.PositionId = request.PositionId;
        assignment.StartDate = request.StartDate;
        assignment.EndDate = request.EndDate;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(assignment.Id, cancellationToken);
    }

    public async Task<Result<EmployeeAssignmentDto>> ChangeCurrentAsync(
        ChangeCurrentEmployeeAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _changeValidator.Validate(request);
        if (!validation.IsValid)
            return Result<EmployeeAssignmentDto>.ValidationFailure(validation.Errors);

        var employee = await _dbContext.Employees.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.EmployeeId, cancellationToken);
        if (employee is null)
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeNotFound(request.EmployeeId));

        var referenceResult = await ValidateReferencesAsync(
            request.DepartmentId, request.PositionId, cancellationToken);
        if (referenceResult is not null)
            return Result<EmployeeAssignmentDto>.Failure(referenceResult);

        var currentAssignments = await _dbContext.EmployeeAssignments
            .Where(item => item.EmployeeId == request.EmployeeId && item.EndDate == null)
            .OrderBy(item => item.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
        if (currentAssignments.Count == 0)
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.CurrentEmployeeAssignmentNotFound);
        if (currentAssignments.Count > 1)
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentStateConflict);

        var current = currentAssignments[0];
        if (current.DepartmentId == request.DepartmentId && current.PositionId == request.PositionId)
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentNoChange);
        if (request.NewStartDate <= current.StartDate)
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentChangeDateInvalid);

        var employeeDateError = ValidateEmployeeDates(employee, request.NewStartDate, null, true);
        if (employeeDateError is not null)
            return Result<EmployeeAssignmentDto>.Failure(employeeDateError);

        // NewStartDate is known to be greater than current.StartDate, so it cannot be MinValue.
        var closingDate = request.NewStartDate.AddDays(-1);
        if (await HasOverlapAsync(
                request.EmployeeId,
                request.NewStartDate,
                null,
                current.Id,
                cancellationToken))
        {
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentOverlap);
        }

        current.EndDate = closingDate;
        var next = new EmployeeAssignment
        {
            Id = Guid.NewGuid(),
            EmployeeId = request.EmployeeId,
            DepartmentId = request.DepartmentId,
            PositionId = request.PositionId,
            StartDate = request.NewStartDate,
            EndDate = null
        };

        await _dbContext.EmployeeAssignments.AddAsync(next, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(next.Id, cancellationToken);
    }

    public async Task<Result<EmployeeAssignmentDto>> CloseAsync(
        Guid id,
        CloseEmployeeAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var assignment = await _dbContext.EmployeeAssignments
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (assignment is null)
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentNotFound);
        if (assignment.EndDate is not null)
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentAlreadyClosed);

        var validation = _closeValidator.Validate(request);
        if (!validation.IsValid)
            return Result<EmployeeAssignmentDto>.ValidationFailure(validation.Errors);
        if (request.EndDate < assignment.StartDate)
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentEndBeforeStart);

        var employee = await _dbContext.Employees.AsNoTracking()
            .SingleAsync(item => item.Id == assignment.EmployeeId, cancellationToken);
        var employeeDateError = ValidateEmployeeDates(
            employee, assignment.StartDate, request.EndDate, false);
        if (employeeDateError is not null)
            return Result<EmployeeAssignmentDto>.Failure(employeeDateError);

        if (await HasOverlapAsync(
                assignment.EmployeeId,
                assignment.StartDate,
                request.EndDate,
                assignment.Id,
                cancellationToken))
        {
            return Result<EmployeeAssignmentDto>.Failure(UseCaseErrors.EmployeeAssignmentOverlap);
        }

        assignment.EndDate = request.EndDate;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(assignment.Id, cancellationToken);
    }

    private IQueryable<EmployeeAssignmentDto> ProjectAssignments() =>
        _dbContext.EmployeeAssignments
            .AsNoTracking()
            .Select(assignment => new EmployeeAssignmentDto(
                assignment.Id,
                assignment.EmployeeId,
                assignment.Employee.EmployeeCode,
                assignment.DepartmentId,
                assignment.Department.Code,
                assignment.Department.Name,
                assignment.PositionId,
                assignment.Position.Code,
                assignment.Position.Name,
                assignment.StartDate,
                assignment.EndDate,
                assignment.EndDate == null));

    private async Task<Error?> ValidateReferencesAsync(
        Guid departmentId,
        Guid positionId,
        CancellationToken cancellationToken)
    {
        var department = await _dbContext.Departments.AsNoTracking()
            .Where(item => item.Id == departmentId)
            .Select(item => new { item.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (department is null)
            return UseCaseErrors.DepartmentNotFound;
        if (!department.IsActive)
            return UseCaseErrors.DepartmentInactive;

        var position = await _dbContext.Positions.AsNoTracking()
            .Where(item => item.Id == positionId)
            .Select(item => new { item.IsActive })
            .SingleOrDefaultAsync(cancellationToken);
        if (position is null)
            return UseCaseErrors.PositionNotFound;
        return !position.IsActive ? UseCaseErrors.PositionInactive : null;
    }

    private async Task<bool> HasOtherOpenAssignmentAsync(
        Guid employeeId,
        Guid? excludedId,
        CancellationToken cancellationToken) =>
        await _dbContext.EmployeeAssignments.AsNoTracking()
            .AnyAsync(
                item => item.EmployeeId == employeeId
                    && item.EndDate == null
                    && (!excludedId.HasValue || item.Id != excludedId.Value),
                cancellationToken);

    private async Task<bool> HasOverlapAsync(
        Guid employeeId,
        DateOnly newStart,
        DateOnly? newEnd,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        var assignments = _dbContext.EmployeeAssignments.AsNoTracking()
            .Where(item => item.EmployeeId == employeeId
                && (!excludedId.HasValue || item.Id != excludedId.Value));

        if (newEnd.HasValue)
        {
            var end = newEnd.Value;
            return await assignments.AnyAsync(
                item => item.StartDate <= end
                    && (item.EndDate == null || item.EndDate >= newStart),
                cancellationToken);
        }

        return await assignments.AnyAsync(
            item => item.EndDate == null || item.EndDate >= newStart,
            cancellationToken);
    }

    private static Error? ValidateEmployeeDates(
        Employee employee,
        DateOnly startDate,
        DateOnly? endDate,
        bool createsOpenAssignment)
    {
        if (startDate < employee.HireDate)
            return UseCaseErrors.EmployeeAssignmentBeforeHireDate;

        if (createsOpenAssignment
            && (employee.EmploymentStatus == EmploymentStatus.Terminated)
                != employee.TerminationDate.HasValue)
        {
            return UseCaseErrors.EmployeeAssignmentEmployeeStateConflict;
        }

        if (employee.TerminationDate is { } terminationDate
            && (startDate > terminationDate || endDate is null || endDate > terminationDate))
        {
            return UseCaseErrors.EmployeeAssignmentAfterTerminationDate;
        }

        return null;
    }
}
