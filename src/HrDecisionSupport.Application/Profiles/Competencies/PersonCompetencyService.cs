using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Profiles.Competencies;

public sealed class PersonCompetencyService : IPersonCompetencyService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreatePersonCompetencyRequest> _createValidator;
    private readonly IValidator<UpdatePersonCompetencyRequest> _updateValidator;

    public PersonCompetencyService(
        IHrDecisionSupportDbContext dbContext,
        IValidator<CreatePersonCompetencyRequest> createValidator,
        IValidator<UpdatePersonCompetencyRequest> updateValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
    }

    public async Task<Result<IReadOnlyList<PersonCompetencyDto>>> ListByPersonAsync(
        Guid personId, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == personId, cancellationToken))
            return Result<IReadOnlyList<PersonCompetencyDto>>.Failure(UseCaseErrors.PersonNotFound);

        var items = await _dbContext.PersonCompetencies
            .AsNoTracking()
            .Where(item => item.PersonId == personId)
            .OrderBy(item => item.Competency.Name)
            .ThenBy(item => item.Id)
            .Select(item => new PersonCompetencyDto(
                item.Id, item.PersonId, item.CompetencyId, item.Competency.Code,
                item.Competency.Name, item.Competency.CompetencyCategory,
                item.ExperienceMonths, item.ProficiencyLevel))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PersonCompetencyDto>>.Success(items);
    }

    public async Task<Result<PersonCompetencyDto>> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.PersonCompetencies
            .AsNoTracking()
            .Where(value => value.Id == id)
            .Select(value => new PersonCompetencyDto(
                value.Id, value.PersonId, value.CompetencyId, value.Competency.Code,
                value.Competency.Name, value.Competency.CompetencyCategory,
                value.ExperienceMonths, value.ProficiencyLevel))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null
            ? Result<PersonCompetencyDto>.Failure(UseCaseErrors.PersonCompetencyNotFound)
            : Result<PersonCompetencyDto>.Success(item);
    }

    public async Task<Result<PersonCompetencyDto>> CreateAsync(
        CreatePersonCompetencyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
            return Result<PersonCompetencyDto>.ValidationFailure(validation.Errors);

        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == request.PersonId, cancellationToken))
            return Result<PersonCompetencyDto>.Failure(UseCaseErrors.PersonNotFound);

        var competency = await _dbContext.Competencies.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.CompetencyId, cancellationToken);
        if (competency is null)
            return Result<PersonCompetencyDto>.Failure(UseCaseErrors.CompetencyNotFound);
        if (!competency.IsActive)
            return Result<PersonCompetencyDto>.Failure(UseCaseErrors.CompetencyInactive);

        if (await _dbContext.PersonCompetencies.AnyAsync(
                item => item.PersonId == request.PersonId && item.CompetencyId == request.CompetencyId,
                cancellationToken))
            return Result<PersonCompetencyDto>.Failure(UseCaseErrors.PersonCompetencyConflict);

        var entity = new PersonCompetency
        {
            Id = Guid.NewGuid(), PersonId = request.PersonId, CompetencyId = request.CompetencyId,
            ExperienceMonths = request.ExperienceMonths, ProficiencyLevel = request.ProficiencyLevel
        };
        await _dbContext.PersonCompetencies.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<PersonCompetencyDto>.Success(Map(entity, competency));
    }

    public async Task<Result<PersonCompetencyDto>> UpdateAsync(
        Guid id, UpdatePersonCompetencyRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _dbContext.PersonCompetencies
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result<PersonCompetencyDto>.Failure(UseCaseErrors.PersonCompetencyNotFound);

        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
            return Result<PersonCompetencyDto>.ValidationFailure(validation.Errors);

        entity.ExperienceMonths = request.ExperienceMonths;
        entity.ProficiencyLevel = request.ProficiencyLevel;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PersonCompetencies
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result.Failure(UseCaseErrors.PersonCompetencyNotFound);
        _dbContext.PersonCompetencies.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static PersonCompetencyDto Map(PersonCompetency entity, Competency competency) =>
        new(entity.Id, entity.PersonId, entity.CompetencyId, competency.Code, competency.Name,
            competency.CompetencyCategory, entity.ExperienceMonths, entity.ProficiencyLevel);
}
