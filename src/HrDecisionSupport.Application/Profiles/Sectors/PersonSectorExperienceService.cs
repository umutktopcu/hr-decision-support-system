using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Profiles.Sectors;

public sealed class PersonSectorExperienceService : IPersonSectorExperienceService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreatePersonSectorExperienceRequest> _createValidator;
    private readonly IValidator<UpdatePersonSectorExperienceRequest> _updateValidator;

    public PersonSectorExperienceService(IHrDecisionSupportDbContext dbContext,
        IValidator<CreatePersonSectorExperienceRequest> createValidator,
        IValidator<UpdatePersonSectorExperienceRequest> updateValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
    }

    public async Task<Result<IReadOnlyList<PersonSectorExperienceDto>>> ListByPersonAsync(Guid personId,
        CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == personId, cancellationToken))
            return Result<IReadOnlyList<PersonSectorExperienceDto>>.Failure(UseCaseErrors.PersonNotFound);
        var items = await _dbContext.PersonSectorExperiences.AsNoTracking()
            .Where(item => item.PersonId == personId)
            .OrderBy(item => item.Sector.Name)
            .ThenBy(item => item.Id)
            .Select(item => new PersonSectorExperienceDto(item.Id, item.PersonId, item.SectorId,
                item.Sector.Code, item.Sector.Name, item.ExperienceMonths, item.Notes))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PersonSectorExperienceDto>>.Success(items);
    }

    public async Task<Result<PersonSectorExperienceDto>> GetByIdAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.PersonSectorExperiences.AsNoTracking()
            .Where(value => value.Id == id)
            .Select(value => new PersonSectorExperienceDto(value.Id, value.PersonId, value.SectorId,
                value.Sector.Code, value.Sector.Name, value.ExperienceMonths, value.Notes))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? Result<PersonSectorExperienceDto>.Failure(UseCaseErrors.PersonSectorExperienceNotFound)
            : Result<PersonSectorExperienceDto>.Success(item);
    }

    public async Task<Result<PersonSectorExperienceDto>> CreateAsync(CreatePersonSectorExperienceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
            return Result<PersonSectorExperienceDto>.ValidationFailure(validation.Errors);
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == request.PersonId, cancellationToken))
            return Result<PersonSectorExperienceDto>.Failure(UseCaseErrors.PersonNotFound);
        var sector = await _dbContext.Sectors.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.SectorId, cancellationToken);
        if (sector is null)
            return Result<PersonSectorExperienceDto>.Failure(UseCaseErrors.SectorNotFound);
        if (await _dbContext.PersonSectorExperiences.AsNoTracking().AnyAsync(
            item => item.PersonId == request.PersonId && item.SectorId == request.SectorId, cancellationToken))
            return Result<PersonSectorExperienceDto>.Failure(UseCaseErrors.PersonSectorExperienceConflict);
        var entity = new PersonSectorExperience
        {
            Id = Guid.NewGuid(), PersonId = request.PersonId, SectorId = request.SectorId,
            ExperienceMonths = request.ExperienceMonths, Notes = request.Notes?.Trim()
        };
        await _dbContext.PersonSectorExperiences.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<PersonSectorExperienceDto>.Success(Map(entity, sector));
    }

    public async Task<Result<PersonSectorExperienceDto>> UpdateAsync(Guid id,
        UpdatePersonSectorExperienceRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _dbContext.PersonSectorExperiences.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result<PersonSectorExperienceDto>.Failure(UseCaseErrors.PersonSectorExperienceNotFound);
        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
            return Result<PersonSectorExperienceDto>.ValidationFailure(validation.Errors);
        entity.ExperienceMonths = request.ExperienceMonths;
        entity.Notes = request.Notes?.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PersonSectorExperiences.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result.Failure(UseCaseErrors.PersonSectorExperienceNotFound);
        _dbContext.PersonSectorExperiences.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static PersonSectorExperienceDto Map(PersonSectorExperience entity, Sector sector) =>
        new(entity.Id, entity.PersonId, entity.SectorId, sector.Code, sector.Name,
            entity.ExperienceMonths, entity.Notes);
}
