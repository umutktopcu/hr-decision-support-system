using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Profiles.Projects;

public sealed class PersonProjectService : IPersonProjectService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreatePersonProjectRequest> _createValidator;
    private readonly IValidator<UpdatePersonProjectRequest> _updateValidator;

    public PersonProjectService(IHrDecisionSupportDbContext dbContext,
        IValidator<CreatePersonProjectRequest> createValidator,
        IValidator<UpdatePersonProjectRequest> updateValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
    }

    public async Task<Result<IReadOnlyList<PersonProjectDto>>> ListByPersonAsync(Guid personId,
        CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == personId, cancellationToken))
            return Result<IReadOnlyList<PersonProjectDto>>.Failure(UseCaseErrors.PersonNotFound);
        var items = await _dbContext.PersonProjects.AsNoTracking()
            .Where(item => item.PersonId == personId)
            .OrderBy(item => item.EndDate != null)
            .ThenBy(item => item.StartDate == null)
            .ThenByDescending(item => item.StartDate)
            .ThenBy(item => item.Project.Name)
            .ThenBy(item => item.Id)
            .Select(item => new PersonProjectDto(item.Id, item.PersonId, item.ProjectId,
                item.Project.Name, item.Role, item.StartDate, item.EndDate, item.Description))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PersonProjectDto>>.Success(items);
    }

    public async Task<Result<PersonProjectDto>> GetByIdAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.PersonProjects.AsNoTracking()
            .Where(value => value.Id == id)
            .Select(value => new PersonProjectDto(value.Id, value.PersonId, value.ProjectId,
                value.Project.Name, value.Role, value.StartDate, value.EndDate, value.Description))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? Result<PersonProjectDto>.Failure(UseCaseErrors.PersonProjectNotFound)
            : Result<PersonProjectDto>.Success(item);
    }

    public async Task<Result<PersonProjectDto>> CreateAsync(CreatePersonProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
            return Result<PersonProjectDto>.ValidationFailure(validation.Errors);
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == request.PersonId, cancellationToken))
            return Result<PersonProjectDto>.Failure(UseCaseErrors.PersonNotFound);
        var project = await _dbContext.Projects.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.ProjectId, cancellationToken);
        if (project is null)
            return Result<PersonProjectDto>.Failure(UseCaseErrors.ProjectNotFound);
        var entity = new PersonProject
        {
            Id = Guid.NewGuid(), PersonId = request.PersonId, ProjectId = request.ProjectId,
            Role = request.Role?.Trim(), StartDate = request.StartDate, EndDate = request.EndDate,
            Description = request.Description?.Trim()
        };
        await _dbContext.PersonProjects.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<PersonProjectDto>.Success(Map(entity, project));
    }

    public async Task<Result<PersonProjectDto>> UpdateAsync(Guid id, UpdatePersonProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _dbContext.PersonProjects.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result<PersonProjectDto>.Failure(UseCaseErrors.PersonProjectNotFound);
        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
            return Result<PersonProjectDto>.ValidationFailure(validation.Errors);
        entity.Role = request.Role?.Trim();
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.Description = request.Description?.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PersonProjects.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result.Failure(UseCaseErrors.PersonProjectNotFound);
        _dbContext.PersonProjects.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static PersonProjectDto Map(PersonProject entity, Project project) =>
        new(entity.Id, entity.PersonId, entity.ProjectId, project.Name, entity.Role,
            entity.StartDate, entity.EndDate, entity.Description);
}
