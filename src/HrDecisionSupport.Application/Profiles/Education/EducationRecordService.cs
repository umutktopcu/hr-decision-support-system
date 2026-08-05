using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Profiles.Education;

public sealed class EducationRecordService : IEducationRecordService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreateEducationRecordRequest> _createValidator;
    private readonly IValidator<UpdateEducationRecordRequest> _updateValidator;

    public EducationRecordService(IHrDecisionSupportDbContext dbContext,
        IValidator<CreateEducationRecordRequest> createValidator,
        IValidator<UpdateEducationRecordRequest> updateValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
    }

    public async Task<Result<IReadOnlyList<EducationRecordDto>>> ListByPersonAsync(
        Guid personId, CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == personId, cancellationToken))
            return Result<IReadOnlyList<EducationRecordDto>>.Failure(UseCaseErrors.PersonNotFound);

        var items = await _dbContext.EducationRecords.AsNoTracking()
            .Where(item => item.PersonId == personId)
            .OrderBy(item => item.GraduationDate == null)
            .ThenByDescending(item => item.GraduationDate)
            .ThenBy(item => item.StartDate == null)
            .ThenByDescending(item => item.StartDate)
            .ThenBy(item => item.Id)
            .Select(item => new EducationRecordDto(item.Id, item.PersonId, item.Institution,
                item.FieldOfStudy, item.DegreeLevel, item.StartDate, item.GraduationDate))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<EducationRecordDto>>.Success(items);
    }

    public async Task<Result<EducationRecordDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.EducationRecords.AsNoTracking()
            .Where(value => value.Id == id)
            .Select(value => new EducationRecordDto(value.Id, value.PersonId, value.Institution,
                value.FieldOfStudy, value.DegreeLevel, value.StartDate, value.GraduationDate))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? Result<EducationRecordDto>.Failure(UseCaseErrors.EducationRecordNotFound)
            : Result<EducationRecordDto>.Success(item);
    }

    public async Task<Result<EducationRecordDto>> CreateAsync(CreateEducationRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
            return Result<EducationRecordDto>.ValidationFailure(validation.Errors);
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == request.PersonId, cancellationToken))
            return Result<EducationRecordDto>.Failure(UseCaseErrors.PersonNotFound);

        var entity = new EducationRecord
        {
            Id = Guid.NewGuid(), PersonId = request.PersonId, Institution = request.Institution.Trim(),
            FieldOfStudy = request.FieldOfStudy?.Trim(), DegreeLevel = request.DegreeLevel,
            StartDate = request.StartDate, GraduationDate = request.GraduationDate
        };
        await _dbContext.EducationRecords.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<EducationRecordDto>.Success(Map(entity));
    }

    public async Task<Result<EducationRecordDto>> UpdateAsync(Guid id, UpdateEducationRecordRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _dbContext.EducationRecords.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result<EducationRecordDto>.Failure(UseCaseErrors.EducationRecordNotFound);
        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
            return Result<EducationRecordDto>.ValidationFailure(validation.Errors);

        entity.Institution = request.Institution.Trim();
        entity.FieldOfStudy = request.FieldOfStudy?.Trim();
        entity.DegreeLevel = request.DegreeLevel;
        entity.StartDate = request.StartDate;
        entity.GraduationDate = request.GraduationDate;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<EducationRecordDto>.Success(Map(entity));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.EducationRecords.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result.Failure(UseCaseErrors.EducationRecordNotFound);
        _dbContext.EducationRecords.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static EducationRecordDto Map(EducationRecord entity) =>
        new(entity.Id, entity.PersonId, entity.Institution, entity.FieldOfStudy,
            entity.DegreeLevel, entity.StartDate, entity.GraduationDate);
}
