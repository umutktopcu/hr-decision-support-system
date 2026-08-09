using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees.Dtos;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Employees;

public sealed class EmployeeService : IEmployeeService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreateEmployeeRequest> _createValidator;
    private readonly IValidator<UpdateEmployeeRequest> _updateValidator;

    public EmployeeService(
        IHrDecisionSupportDbContext dbContext,
        IValidator<CreateEmployeeRequest> createValidator,
        IValidator<UpdateEmployeeRequest> updateValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
    }

    public async Task<Result<IReadOnlyList<EmployeeListItemDto>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var employees = await _dbContext.Employees
            .AsNoTracking()
            .OrderBy(employee => employee.EmployeeCode)
            .ThenBy(employee => employee.Id)
            .Select(employee => new EmployeeListItemDto(
                employee.Id,
                employee.EmployeeCode,
                employee.Person.AnonymousCode,
                employee.Person.FirstName,
                employee.Person.LastName,
                employee.Person.Email,
                employee.HireDate,
                employee.TerminationDate,
                employee.EmploymentStatus,
                employee.Assignments
                    .Where(assignment => assignment.EndDate == null)
                    .OrderByDescending(assignment => assignment.StartDate)
                    .ThenByDescending(assignment => assignment.Id)
                    .Select(assignment => (Guid?)assignment.DepartmentId)
                    .FirstOrDefault(),
                employee.Assignments
                    .Where(assignment => assignment.EndDate == null)
                    .OrderByDescending(assignment => assignment.StartDate)
                    .ThenByDescending(assignment => assignment.Id)
                    .Select(assignment => assignment.Department.Name)
                    .FirstOrDefault(),
                employee.Assignments
                    .Where(assignment => assignment.EndDate == null)
                    .OrderByDescending(assignment => assignment.StartDate)
                    .ThenByDescending(assignment => assignment.Id)
                    .Select(assignment => (Guid?)assignment.PositionId)
                    .FirstOrDefault(),
                employee.Assignments
                    .Where(assignment => assignment.EndDate == null)
                    .OrderByDescending(assignment => assignment.StartDate)
                    .ThenByDescending(assignment => assignment.Id)
                    .Select(assignment => assignment.Position.Name)
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);

        return Result<IReadOnlyList<EmployeeListItemDto>>.Success(employees);
    }

    public async Task<Result<EmployeeDetailsDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var employee = await _dbContext.Employees
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new EmployeeHeader(
                item.Id,
                item.PersonId,
                item.EmployeeCode,
                item.Person.AnonymousCode,
                item.Person.FirstName,
                item.Person.LastName,
                item.Person.Email,
                item.Person.PhoneNumber,
                item.HireDate,
                item.TerminationDate,
                item.EmploymentStatus))
            .SingleOrDefaultAsync(cancellationToken);

        if (employee is null)
        {
            return Result<EmployeeDetailsDto>.Failure(UseCaseErrors.EmployeeNotFound(id));
        }

        var assignments = await _dbContext.EmployeeAssignments
            .AsNoTracking()
            .Where(assignment => assignment.EmployeeId == id)
            .OrderByDescending(assignment => assignment.StartDate)
            .ThenByDescending(assignment => assignment.Id)
            .Select(assignment => new EmployeeAssignmentDto(
                assignment.Id,
                assignment.DepartmentId,
                assignment.Department.Name,
                assignment.PositionId,
                assignment.Position.Name,
                assignment.StartDate,
                assignment.EndDate))
            .ToListAsync(cancellationToken);

        // --- YENİ EKLENEN KISIM: Snapshot Verilerini Çekiyoruz ---
        // PersonId üzerinden ilgili kariyer snapshot verisini buluyoruz
        // Çalışanın PersonId'sine karşılık gelen bir aday (Candidate) ve onun kariyer snapshot verisini çekiyoruz
        int? shortestJob = null;
        int? totalExp = null;

        var candidateRecord = await _dbContext.Candidates
            .AsNoTracking()
            .Include(c => c.CareerFeatureSnapshots)
            .FirstOrDefaultAsync(c => c.PersonId == employee.PersonId, cancellationToken);

        if (candidateRecord != null && candidateRecord.CareerFeatureSnapshots != null)
        {
            var snap = candidateRecord.CareerFeatureSnapshots
                .OrderByDescending(s => s.TotalExperienceMonths)
                .FirstOrDefault();

            if (snap != null)
            {
                shortestJob = (int?)snap.ShortestPreviousJobMonths;
                totalExp = (int?)snap.TotalExperienceMonths;
            }
        }
        // -----------------------------------------------------------

        return Result<EmployeeDetailsDto>.Success(MapDetails(employee, assignments, shortestJob, totalExp));
    }

    public async Task<Result<EmployeeDetailsDto>> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
        {
            return Result<EmployeeDetailsDto>.ValidationFailure(validation.Errors);
        }

        var employeeCode = request.EmployeeCode.Trim();
        var anonymousCode = request.AnonymousCode.Trim();
        var personData = PersonData.Create(
            request.FirstName,
            request.LastName,
            request.Email,
            request.PhoneNumber);

        if (await _dbContext.Employees.AnyAsync(
                employee => employee.EmployeeCode == employeeCode,
                cancellationToken))
        {
            return Result<EmployeeDetailsDto>.Failure(UseCaseErrors.EmployeeCodeConflict);
        }

        var department = await _dbContext.Departments
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == request.InitialDepartmentId,
                cancellationToken);
        if (department is null)
        {
            return Result<EmployeeDetailsDto>.Failure(UseCaseErrors.DepartmentNotFound);
        }

        if (!department.IsActive)
        {
            return Result<EmployeeDetailsDto>.Failure(UseCaseErrors.DepartmentInactive);
        }

        var position = await _dbContext.Positions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == request.InitialPositionId,
                cancellationToken);
        if (position is null)
        {
            return Result<EmployeeDetailsDto>.Failure(UseCaseErrors.PositionNotFound);
        }

        if (!position.IsActive)
        {
            return Result<EmployeeDetailsDto>.Failure(UseCaseErrors.PositionInactive);
        }

        var person = await _dbContext.People.SingleOrDefaultAsync(
            item => item.AnonymousCode == anonymousCode,
            cancellationToken);
        var utcNow = DateTime.UtcNow;

        if (person is null)
        {
            person = personData.CreatePerson(anonymousCode, utcNow);
            await _dbContext.People.AddAsync(person, cancellationToken);
        }
        else
        {
            if (await _dbContext.Employees.AnyAsync(
                    employee => employee.PersonId == person.Id,
                    cancellationToken))
            {
                return Result<EmployeeDetailsDto>.Failure(UseCaseErrors.PersonAlreadyEmployee);
            }

            if (personData.ConflictsWith(person))
            {
                return Result<EmployeeDetailsDto>.Failure(UseCaseErrors.PersonDataConflict);
            }

            if (personData.CompleteMissingFields(person))
            {
                person.UpdatedAtUtc = utcNow;
            }
        }

        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            Person = person,
            EmployeeCode = employeeCode,
            HireDate = request.HireDate,
            TerminationDate = request.TerminationDate,
            EmploymentStatus = request.EmploymentStatus
        };
        var assignment = new EmployeeAssignment
        {
            Id = Guid.NewGuid(),
            EmployeeId = employee.Id,
            Employee = employee,
            DepartmentId = department.Id,
            PositionId = position.Id,
            StartDate = request.InitialAssignmentStartDate ?? request.HireDate,
            EndDate = null
        };

        await _dbContext.Employees.AddAsync(employee, cancellationToken);
        await _dbContext.EmployeeAssignments.AddAsync(assignment, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var assignmentDto = new EmployeeAssignmentDto(
            assignment.Id,
            department.Id,
            department.Name,
            position.Id,
            position.Name,
            assignment.StartDate,
            assignment.EndDate);

        return Result<EmployeeDetailsDto>.Success(MapDetails(employee, person, [assignmentDto]));
    }

    public async Task<Result<EmployeeDetailsDto>> UpdateAsync(
        Guid id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var employee = await _dbContext.Employees
            .Include(item => item.Person)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (employee is null)
        {
            return Result<EmployeeDetailsDto>.Failure(UseCaseErrors.EmployeeNotFound(id));
        }

        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
        {
            return Result<EmployeeDetailsDto>.ValidationFailure(validation.Errors);
        }

        var employeeCode = request.EmployeeCode.Trim();
        if (employee.EmployeeCode != employeeCode
            && await _dbContext.Employees.AnyAsync(
                item => item.Id != id && item.EmployeeCode == employeeCode,
                cancellationToken))
        {
            return Result<EmployeeDetailsDto>.Failure(UseCaseErrors.EmployeeCodeConflict);
        }

        employee.EmployeeCode = employeeCode;
        employee.HireDate = request.HireDate;
        employee.TerminationDate = request.TerminationDate;
        employee.EmploymentStatus = request.EmploymentStatus;

        PersonData.Create(request.FirstName, request.LastName, request.Email, request.PhoneNumber)
            .ApplyTo(employee.Person);
        employee.Person.UpdatedAtUtc = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    private static EmployeeDetailsDto MapDetails(
        Employee employee,
        Person person,
        IReadOnlyList<EmployeeAssignmentDto> assignments,
        int? shortestJobMonths = null,          // <-- Varsayılan değer eklendi
        int? totalExperienceMonths = null)      // <-- Varsayılan değer eklendi
        => new(
            employee.Id,
            person.Id,
            employee.EmployeeCode,
            person.AnonymousCode,
            person.FirstName,
            person.LastName,
            person.Email,
            person.PhoneNumber,
            employee.HireDate,
            employee.TerminationDate,
            employee.EmploymentStatus,
            assignments,
            shortestJobMonths,
            totalExperienceMonths
        );

    private static EmployeeDetailsDto MapDetails(
        EmployeeHeader employee,
        IReadOnlyList<EmployeeAssignmentDto> assignments,
        int? shortestJobMonths = null,          // <-- Varsayılan değer eklendi
        int? totalExperienceMonths = null)      // <-- Varsayılan değer eklendi
        => new(
            employee.Id,
            employee.PersonId,
            employee.EmployeeCode,
            employee.AnonymousCode,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.PhoneNumber,
            employee.HireDate,
            employee.TerminationDate,
            employee.EmploymentStatus,
            assignments,
            shortestJobMonths,
            totalExperienceMonths
        );

    private static EmployeeDetailsDto MapDetails(
        EmployeeHeader employee,
        IReadOnlyList<EmployeeAssignmentDto> assignments) =>
        new(
            employee.Id,
            employee.PersonId,
            employee.EmployeeCode,
            employee.AnonymousCode,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.PhoneNumber,
            employee.HireDate,
            employee.TerminationDate,
            employee.EmploymentStatus,
            assignments);

    private sealed record EmployeeHeader(
        Guid Id,
        Guid PersonId,
        string EmployeeCode,
        string AnonymousCode,
        string? FirstName,
        string? LastName,
        string? Email,
        string? PhoneNumber,
        DateOnly HireDate,
        DateOnly? TerminationDate,
        EmploymentStatus EmploymentStatus);
}
