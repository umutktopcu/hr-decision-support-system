using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.CandidateEvaluations;

public sealed class CandidateEvaluationCaseService : ICandidateEvaluationCaseService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreateCandidateEvaluationCaseRequest> _createValidator;
    private readonly IValidator<UpdateCandidateEvaluationCaseRequest> _updateValidator;
    private readonly IValidator<ChangeCandidateEvaluationCaseStatusRequest> _statusValidator;
    private readonly TimeProvider _timeProvider;

    public CandidateEvaluationCaseService(
        IHrDecisionSupportDbContext dbContext,
        IValidator<CreateCandidateEvaluationCaseRequest> createValidator,
        IValidator<UpdateCandidateEvaluationCaseRequest> updateValidator,
        IValidator<ChangeCandidateEvaluationCaseStatusRequest> statusValidator,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
        _statusValidator = statusValidator ?? throw new ArgumentNullException(nameof(statusValidator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<IReadOnlyList<CandidateEvaluationCaseDto>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var items = await Order(ProjectCases()).ToListAsync(cancellationToken);
        return Result<IReadOnlyList<CandidateEvaluationCaseDto>>.Success(items);
    }

    public async Task<Result<IReadOnlyList<CandidateEvaluationCaseDto>>> ListByRequisitionAsync(
        Guid jobRequisitionId,
        CancellationToken cancellationToken = default)
    {
        if (jobRequisitionId == Guid.Empty)
        {
            return Result<IReadOnlyList<CandidateEvaluationCaseDto>>.ValidationFailure(
                RequiredIdError("job_requisition_id_required", "JobRequisitionId"));
        }

        if (!await _dbContext.JobRequisitions.AsNoTracking().AnyAsync(
                item => item.Id == jobRequisitionId,
                cancellationToken))
        {
            return Result<IReadOnlyList<CandidateEvaluationCaseDto>>.Failure(
                UseCaseErrors.JobRequisitionNotFound);
        }

        var items = await Order(ProjectCases().Where(
                item => item.JobRequisitionId == jobRequisitionId))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<CandidateEvaluationCaseDto>>.Success(items);
    }

    public async Task<Result<IReadOnlyList<CandidateEvaluationCaseDto>>> ListByCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default)
    {
        if (candidateId == Guid.Empty)
        {
            return Result<IReadOnlyList<CandidateEvaluationCaseDto>>.ValidationFailure(
                RequiredIdError("candidate_id_required", "CandidateId"));
        }

        if (!await _dbContext.Candidates.AsNoTracking().AnyAsync(
                item => item.Id == candidateId,
                cancellationToken))
        {
            return Result<IReadOnlyList<CandidateEvaluationCaseDto>>.Failure(
                UseCaseErrors.CandidateNotFound);
        }

        var items = await Order(ProjectCases().Where(item => item.CandidateId == candidateId))
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<CandidateEvaluationCaseDto>>.Success(items);
    }

    public async Task<Result<CandidateEvaluationCaseDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await _dbContext.CandidateEvaluationCases
            .AsNoTracking()
            .Where(value => value.Id == id)
            .Select(value => new CandidateEvaluationCaseDetailDto(
                value.Id,
                value.CandidateId,
                value.Candidate.CandidateCode,
                value.Candidate.Person.AnonymousCode,
                value.Candidate.Person.FirstName,
                value.Candidate.Person.LastName,
                value.JobRequisitionId,
                value.JobRequisition.RequisitionCode,
                value.JobRequisition.Title,
                value.JobRequisition.DepartmentId,
                value.JobRequisition.Department.Name,
                value.JobRequisition.PositionId,
                value.JobRequisition.Position.Name,
                value.ExternalReference,
                value.ReceivedAtUtc,
                value.Status,
                value.Notes,
                value.CreatedAtUtc,
                value.UpdatedAtUtc))
            .SingleOrDefaultAsync(cancellationToken);

        return item is null
            ? Result<CandidateEvaluationCaseDetailDto>.Failure(
                UseCaseErrors.CandidateEvaluationCaseNotFound)
            : Result<CandidateEvaluationCaseDetailDto>.Success(item);
    }

    public async Task<Result<CandidateEvaluationCaseDto>> CreateAsync(
        CreateCandidateEvaluationCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
            return Result<CandidateEvaluationCaseDto>.ValidationFailure(validation.Errors);

        var personId = await _dbContext.Candidates.AsNoTracking()
            .Where(item => item.Id == request.CandidateId)
            .Select(item => (Guid?)item.PersonId)
            .SingleOrDefaultAsync(cancellationToken);
        if (!personId.HasValue
            || !await _dbContext.People.AsNoTracking().AnyAsync(
                item => item.Id == personId.Value,
                cancellationToken))
        {
            return Result<CandidateEvaluationCaseDto>.Failure(UseCaseErrors.CandidateNotFound);
        }

        var requisitionStatus = await _dbContext.JobRequisitions.AsNoTracking()
            .Where(item => item.Id == request.JobRequisitionId)
            .Select(item => (JobRequisitionStatus?)item.JobRequisitionStatus)
            .SingleOrDefaultAsync(cancellationToken);
        if (!requisitionStatus.HasValue)
            return Result<CandidateEvaluationCaseDto>.Failure(UseCaseErrors.JobRequisitionNotFound);
        if (requisitionStatus is not (JobRequisitionStatus.Open or JobRequisitionStatus.OnHold))
        {
            return Result<CandidateEvaluationCaseDto>.Failure(
                UseCaseErrors.CandidateEvaluationCaseRequisitionNotAvailable);
        }

        if (await _dbContext.CandidateEvaluationCases.AsNoTracking().AnyAsync(
                item => item.CandidateId == request.CandidateId
                    && item.JobRequisitionId == request.JobRequisitionId,
                cancellationToken))
        {
            return Result<CandidateEvaluationCaseDto>.Failure(
                UseCaseErrors.CandidateEvaluationCaseConflict);
        }

        var entity = new CandidateEvaluationCase
        {
            Id = Guid.NewGuid(),
            CandidateId = request.CandidateId,
            JobRequisitionId = request.JobRequisitionId,
            ExternalReference = request.ExternalReference?.Trim(),
            ReceivedAtUtc = request.ReceivedAtUtc,
            Status = CandidateEvaluationStatus.New,
            Notes = request.Notes?.Trim(),
            CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        };

        await _dbContext.CandidateEvaluationCases.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetDtoByIdAsync(entity.Id, cancellationToken);
    }

    public async Task<Result<CandidateEvaluationCaseDto>> UpdateAsync(
        Guid id,
        UpdateCandidateEvaluationCaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _dbContext.CandidateEvaluationCases
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result<CandidateEvaluationCaseDto>.Failure(
                UseCaseErrors.CandidateEvaluationCaseNotFound);
        }

        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
            return Result<CandidateEvaluationCaseDto>.ValidationFailure(validation.Errors);
        if (IsTerminal(entity.Status))
        {
            return Result<CandidateEvaluationCaseDto>.Failure(
                UseCaseErrors.CandidateEvaluationCaseLocked);
        }

        entity.ExternalReference = request.ExternalReference?.Trim();
        entity.ReceivedAtUtc = request.ReceivedAtUtc;
        entity.Notes = request.Notes?.Trim();
        entity.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetDtoByIdAsync(id, cancellationToken);
    }

    public async Task<Result<CandidateEvaluationCaseDto>> ChangeStatusAsync(
        Guid id,
        ChangeCandidateEvaluationCaseStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _statusValidator.Validate(request);
        if (!validation.IsValid)
            return Result<CandidateEvaluationCaseDto>.ValidationFailure(validation.Errors);

        var entity = await _dbContext.CandidateEvaluationCases
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result<CandidateEvaluationCaseDto>.Failure(
                UseCaseErrors.CandidateEvaluationCaseNotFound);
        }
        if (entity.Status == request.Status)
        {
            return Result<CandidateEvaluationCaseDto>.Failure(
                UseCaseErrors.CandidateEvaluationCaseStatusNoChange);
        }
        if (!CanTransition(entity.Status, request.Status))
        {
            return Result<CandidateEvaluationCaseDto>.Failure(
                UseCaseErrors.CandidateEvaluationCaseStatusTransitionInvalid);
        }

        entity.Status = request.Status;
        entity.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetDtoByIdAsync(id, cancellationToken);
    }

    private IQueryable<CandidateEvaluationCaseDto> ProjectCases() =>
        _dbContext.CandidateEvaluationCases
            .AsNoTracking()
            .Select(value => new CandidateEvaluationCaseDto(
                value.Id,
                value.CandidateId,
                value.Candidate.CandidateCode,
                value.Candidate.Person.AnonymousCode,
                value.Candidate.Person.FirstName,
                value.Candidate.Person.LastName,
                value.JobRequisitionId,
                value.JobRequisition.RequisitionCode,
                value.JobRequisition.Title,
                value.JobRequisition.DepartmentId,
                value.JobRequisition.Department.Name,
                value.JobRequisition.PositionId,
                value.JobRequisition.Position.Name,
                value.ExternalReference,
                value.ReceivedAtUtc,
                value.Status,
                value.Notes,
                value.CreatedAtUtc,
                value.UpdatedAtUtc));

    private static IOrderedQueryable<CandidateEvaluationCaseDto> Order(
        IQueryable<CandidateEvaluationCaseDto> query) =>
        query.OrderByDescending(item => item.Status == CandidateEvaluationStatus.New
                || item.Status == CandidateEvaluationStatus.InReview
                || item.Status == CandidateEvaluationStatus.Interview)
            .ThenByDescending(item => item.ReceivedAtUtc)
            .ThenBy(item => item.Id);

    private async Task<Result<CandidateEvaluationCaseDto>> GetDtoByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var item = await ProjectCases().SingleAsync(value => value.Id == id, cancellationToken);
        return Result<CandidateEvaluationCaseDto>.Success(item);
    }

    private static ValidationError RequiredIdError(string code, string propertyName) =>
        new(code, $"{propertyName} must not be empty.", propertyName);

    private static bool IsTerminal(CandidateEvaluationStatus status) =>
        status is CandidateEvaluationStatus.Approved
            or CandidateEvaluationStatus.Rejected
            or CandidateEvaluationStatus.Withdrawn
            or CandidateEvaluationStatus.Closed;

    private static bool CanTransition(
        CandidateEvaluationStatus current,
        CandidateEvaluationStatus next) =>
        current switch
        {
            CandidateEvaluationStatus.New => next is CandidateEvaluationStatus.InReview
                or CandidateEvaluationStatus.Rejected
                or CandidateEvaluationStatus.Withdrawn
                or CandidateEvaluationStatus.Closed,
            CandidateEvaluationStatus.InReview => next is CandidateEvaluationStatus.Interview
                or CandidateEvaluationStatus.Approved
                or CandidateEvaluationStatus.Rejected
                or CandidateEvaluationStatus.Withdrawn
                or CandidateEvaluationStatus.Closed,
            CandidateEvaluationStatus.Interview => next is CandidateEvaluationStatus.Approved
                or CandidateEvaluationStatus.Rejected
                or CandidateEvaluationStatus.Withdrawn
                or CandidateEvaluationStatus.Closed,
            _ => false
        };
}
