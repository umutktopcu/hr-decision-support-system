using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Profiles.WorkModes;

public sealed class PersonWorkModeExperienceService : IPersonWorkModeExperienceService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreatePersonWorkModeExperienceRequest> _createValidator;
    private readonly IValidator<UpdatePersonWorkModeExperienceRequest> _updateValidator;

    public PersonWorkModeExperienceService(IHrDecisionSupportDbContext dbContext,
        IValidator<CreatePersonWorkModeExperienceRequest> createValidator,
        IValidator<UpdatePersonWorkModeExperienceRequest> updateValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
    }

    public async Task<Result<IReadOnlyList<PersonWorkModeExperienceDto>>> ListByPersonAsync(Guid personId,
        CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == personId, cancellationToken))
            return Result<IReadOnlyList<PersonWorkModeExperienceDto>>.Failure(UseCaseErrors.PersonNotFound);
        var items = await _dbContext.PersonWorkModeExperiences.AsNoTracking()
            .Where(item => item.PersonId == personId)
            .OrderBy(item => item.WorkMode.Name)
            .ThenBy(item => item.Id)
            .Select(item => new PersonWorkModeExperienceDto(item.Id, item.PersonId, item.WorkModeId,
                item.WorkMode.Code, item.WorkMode.Name, item.ExperienceMonths))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PersonWorkModeExperienceDto>>.Success(items);
    }

    public async Task<Result<PersonWorkModeExperienceDto>> GetByIdAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.PersonWorkModeExperiences.AsNoTracking()
            .Where(value => value.Id == id)
            .Select(value => new PersonWorkModeExperienceDto(value.Id, value.PersonId, value.WorkModeId,
                value.WorkMode.Code, value.WorkMode.Name, value.ExperienceMonths))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? Result<PersonWorkModeExperienceDto>.Failure(UseCaseErrors.PersonWorkModeExperienceNotFound)
            : Result<PersonWorkModeExperienceDto>.Success(item);
    }

    public async Task<Result<PersonWorkModeExperienceDto>> CreateAsync(CreatePersonWorkModeExperienceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
            return Result<PersonWorkModeExperienceDto>.ValidationFailure(validation.Errors);
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == request.PersonId, cancellationToken))
            return Result<PersonWorkModeExperienceDto>.Failure(UseCaseErrors.PersonNotFound);
        var workMode = await _dbContext.WorkModes.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.WorkModeId, cancellationToken);
        if (workMode is null)
            return Result<PersonWorkModeExperienceDto>.Failure(UseCaseErrors.WorkModeNotFound);
        if (await _dbContext.PersonWorkModeExperiences.AsNoTracking().AnyAsync(
            item => item.PersonId == request.PersonId && item.WorkModeId == request.WorkModeId, cancellationToken))
            return Result<PersonWorkModeExperienceDto>.Failure(UseCaseErrors.PersonWorkModeExperienceConflict);
        var entity = new PersonWorkModeExperience
        {
            Id = Guid.NewGuid(), PersonId = request.PersonId, WorkModeId = request.WorkModeId,
            ExperienceMonths = request.ExperienceMonths
        };
        await _dbContext.PersonWorkModeExperiences.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<PersonWorkModeExperienceDto>.Success(Map(entity, workMode));
    }

    public async Task<Result<PersonWorkModeExperienceDto>> UpdateAsync(Guid id,
        UpdatePersonWorkModeExperienceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _dbContext.PersonWorkModeExperiences.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result<PersonWorkModeExperienceDto>.Failure(UseCaseErrors.PersonWorkModeExperienceNotFound);
        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
            return Result<PersonWorkModeExperienceDto>.ValidationFailure(validation.Errors);
        entity.ExperienceMonths = request.ExperienceMonths;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PersonWorkModeExperiences.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result.Failure(UseCaseErrors.PersonWorkModeExperienceNotFound);
        _dbContext.PersonWorkModeExperiences.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static PersonWorkModeExperienceDto Map(PersonWorkModeExperience entity, WorkMode workMode) =>
        new(entity.Id, entity.PersonId, entity.WorkModeId, workMode.Code, workMode.Name,
            entity.ExperienceMonths);
}
