using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Profiles.EmploymentHistory;

public sealed class EmploymentHistoryService : IEmploymentHistoryService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreateEmploymentHistoryRequest> _createValidator;
    private readonly IValidator<UpdateEmploymentHistoryRequest> _updateValidator;

    public EmploymentHistoryService(IHrDecisionSupportDbContext dbContext,
        IValidator<CreateEmploymentHistoryRequest> createValidator,
        IValidator<UpdateEmploymentHistoryRequest> updateValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
    }

    public async Task<Result<IReadOnlyList<EmploymentHistoryDto>>> ListByPersonAsync(Guid personId,
        CancellationToken cancellationToken = default)
    {
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == personId, cancellationToken))
            return Result<IReadOnlyList<EmploymentHistoryDto>>.Failure(UseCaseErrors.PersonNotFound);
        var items = await _dbContext.EmploymentHistories.AsNoTracking()
            .Where(item => item.PersonId == personId)
            .OrderBy(item => item.EndDate != null)
            .ThenByDescending(item => item.StartDate)
            .ThenBy(item => item.Id)
            .Select(item => new EmploymentHistoryDto(item.Id, item.PersonId, item.EmployerName,
                item.PositionTitle, item.StartDate, item.EndDate, item.Description))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<EmploymentHistoryDto>>.Success(items);
    }

    public async Task<Result<EmploymentHistoryDto>> GetByIdAsync(Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.EmploymentHistories.AsNoTracking()
            .Where(value => value.Id == id)
            .Select(value => new EmploymentHistoryDto(value.Id, value.PersonId, value.EmployerName,
                value.PositionTitle, value.StartDate, value.EndDate, value.Description))
            .SingleOrDefaultAsync(cancellationToken);
        return item is null ? Result<EmploymentHistoryDto>.Failure(UseCaseErrors.EmploymentHistoryNotFound)
            : Result<EmploymentHistoryDto>.Success(item);
    }

    public async Task<Result<EmploymentHistoryDto>> CreateAsync(CreateEmploymentHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
            return Result<EmploymentHistoryDto>.ValidationFailure(validation.Errors);
        if (!await _dbContext.People.AsNoTracking().AnyAsync(person => person.Id == request.PersonId, cancellationToken))
            return Result<EmploymentHistoryDto>.Failure(UseCaseErrors.PersonNotFound);
        var entity = new Domain.Entities.EmploymentHistory
        {
            Id = Guid.NewGuid(), PersonId = request.PersonId, EmployerName = request.EmployerName.Trim(),
            PositionTitle = request.PositionTitle.Trim(), StartDate = request.StartDate,
            EndDate = request.EndDate, Description = request.Description?.Trim()
        };
        await _dbContext.EmploymentHistories.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<EmploymentHistoryDto>.Success(Map(entity));
    }

    public async Task<Result<EmploymentHistoryDto>> UpdateAsync(Guid id, UpdateEmploymentHistoryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _dbContext.EmploymentHistories.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result<EmploymentHistoryDto>.Failure(UseCaseErrors.EmploymentHistoryNotFound);
        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
            return Result<EmploymentHistoryDto>.ValidationFailure(validation.Errors);
        entity.EmployerName = request.EmployerName.Trim();
        entity.PositionTitle = request.PositionTitle.Trim();
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.Description = request.Description?.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<EmploymentHistoryDto>.Success(Map(entity));
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.EmploymentHistories.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result.Failure(UseCaseErrors.EmploymentHistoryNotFound);
        _dbContext.EmploymentHistories.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static EmploymentHistoryDto Map(Domain.Entities.EmploymentHistory entity) =>
        new(entity.Id, entity.PersonId, entity.EmployerName, entity.PositionTitle,
            entity.StartDate, entity.EndDate, entity.Description);
}
