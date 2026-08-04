using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Profiles.Languages;

public sealed class PersonLanguageService : IPersonLanguageService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreatePersonLanguageRequest> _createValidator;
    private readonly IValidator<UpdatePersonLanguageRequest> _updateValidator;

    public PersonLanguageService(IHrDecisionSupportDbContext dbContext,
        IValidator<CreatePersonLanguageRequest> createValidator,
        IValidator<UpdatePersonLanguageRequest> updateValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
    }

    public async Task<Result<IReadOnlyList<PersonLanguageDto>>> ListByPersonAsync(
        Guid personId, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == personId, cancellationToken))
            return Result<IReadOnlyList<PersonLanguageDto>>.Failure(UseCaseErrors.PersonNotFound);
        var items = await _dbContext.PersonLanguages.AsNoTracking()
            .Where(item => item.PersonId == personId)
            .OrderBy(item => item.Language.Name)
            .ThenBy(item => item.Id)
            .Select(item => new PersonLanguageDto(item.Id, item.PersonId, item.LanguageId,
                item.Language.Code, item.Language.Name, item.ProficiencyLevel, item.IsNative))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<PersonLanguageDto>>.Success(items);
    }

    public async Task<Result<PersonLanguageDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.PersonLanguages.AsNoTracking()
            .Where(value => value.Id == id)
            .Select(value => new PersonLanguageDto(value.Id, value.PersonId, value.LanguageId,
                value.Language.Code, value.Language.Name, value.ProficiencyLevel, value.IsNative))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? Result<PersonLanguageDto>.Failure(UseCaseErrors.PersonLanguageNotFound)
            : Result<PersonLanguageDto>.Success(item);
    }

    public async Task<Result<PersonLanguageDto>> CreateAsync(CreatePersonLanguageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
            return Result<PersonLanguageDto>.ValidationFailure(validation.Errors);
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == request.PersonId, cancellationToken))
            return Result<PersonLanguageDto>.Failure(UseCaseErrors.PersonNotFound);
        var language = await _dbContext.Languages.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == request.LanguageId, cancellationToken);
        if (language is null)
            return Result<PersonLanguageDto>.Failure(UseCaseErrors.LanguageNotFound);
        if (await _dbContext.PersonLanguages.AnyAsync(
                item => item.PersonId == request.PersonId && item.LanguageId == request.LanguageId,
                cancellationToken))
            return Result<PersonLanguageDto>.Failure(UseCaseErrors.PersonLanguageConflict);

        var entity = new PersonLanguage
        {
            Id = Guid.NewGuid(), PersonId = request.PersonId, LanguageId = request.LanguageId,
            ProficiencyLevel = request.ProficiencyLevel, IsNative = request.IsNative
        };
        await _dbContext.PersonLanguages.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<PersonLanguageDto>.Success(Map(entity, language));
    }

    public async Task<Result<PersonLanguageDto>> UpdateAsync(Guid id, UpdatePersonLanguageRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _dbContext.PersonLanguages.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result<PersonLanguageDto>.Failure(UseCaseErrors.PersonLanguageNotFound);
        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
            return Result<PersonLanguageDto>.ValidationFailure(validation.Errors);
        entity.ProficiencyLevel = request.ProficiencyLevel;
        entity.IsNative = request.IsNative;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.PersonLanguages.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result.Failure(UseCaseErrors.PersonLanguageNotFound);
        _dbContext.PersonLanguages.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static PersonLanguageDto Map(PersonLanguage entity, Language language) =>
        new(entity.Id, entity.PersonId, entity.LanguageId, language.Code, language.Name,
            entity.ProficiencyLevel, entity.IsNative);
}
