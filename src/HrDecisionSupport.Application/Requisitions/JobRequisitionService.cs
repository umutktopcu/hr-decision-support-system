using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Requisitions.Requirements;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Requisitions;

public sealed class JobRequisitionService : IJobRequisitionService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreateJobRequisitionRequest> _createValidator;
    private readonly IValidator<UpdateJobRequisitionRequest> _updateValidator;
    private readonly IValidator<ChangeJobRequisitionStatusRequest> _statusValidator;
    private readonly TimeProvider _timeProvider;

    public JobRequisitionService(
        IHrDecisionSupportDbContext dbContext,
        IValidator<CreateJobRequisitionRequest> createValidator,
        IValidator<UpdateJobRequisitionRequest> updateValidator,
        IValidator<ChangeJobRequisitionStatusRequest> statusValidator,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
        _statusValidator = statusValidator ?? throw new ArgumentNullException(nameof(statusValidator));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<Result<IReadOnlyList<JobRequisitionDto>>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.JobRequisitions.AsNoTracking();
        var items = await ProjectRequisitions(query)
            .OrderByDescending(item => item.JobRequisitionStatus == JobRequisitionStatus.Open)
            .ThenByDescending(item => item.OpenedAt)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<JobRequisitionDto>>.Success(items);
    }

    public async Task<Result<JobRequisitionDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.JobRequisitions.AsNoTracking().Where(item => item.Id == id);
        var requisition = await ProjectRequisitions(query)
            .SingleOrDefaultAsync(cancellationToken);
        if (requisition is null)
            return Result<JobRequisitionDetailDto>.Failure(UseCaseErrors.JobRequisitionNotFound);

        var requirements = await _dbContext.JobRequisitionRequirements
            .AsNoTracking()
            .Where(item => item.JobRequisitionId == id)
            .OrderByDescending(item => item.IsRequired)
            .ThenBy(item => item.Competency.Name)
            .ThenBy(item => item.Id)
            .Select(item => new JobRequisitionRequirementDto(
                item.Id,
                item.JobRequisitionId,
                item.CompetencyId,
                item.Competency.Code,
                item.Competency.Name,
                item.Competency.CompetencyCategory,
                item.MinimumExperienceMonths,
                item.MinimumProficiencyLevel,
                item.IsRequired,
                item.Notes))
            .ToListAsync(cancellationToken);

        return Result<JobRequisitionDetailDto>.Success(new(
            requisition.Id,
            requisition.RequisitionCode,
            requisition.Title,
            requisition.DepartmentId,
            requisition.DepartmentCode,
            requisition.DepartmentName,
            requisition.PositionId,
            requisition.PositionCode,
            requisition.PositionName,
            requisition.Description,
            requisition.OpeningsCount,
            requisition.JobRequisitionStatus,
            requisition.OpenedAt,
            requisition.ClosedAt,
            requisition.CreatedAtUtc,
            requisition.UpdatedAtUtc,
            requisition.RequirementCount,
            requirements));
    }

    public async Task<Result<JobRequisitionDto>> CreateAsync(
        CreateJobRequisitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
            return Result<JobRequisitionDto>.ValidationFailure(validation.Errors);

        var references = await ValidateReferencesAsync(
            request.DepartmentId,
            request.PositionId,
            cancellationToken);
        if (references.Error is not null)
            return Result<JobRequisitionDto>.Failure(references.Error);

        var requisitionCode = request.RequisitionCode.Trim();
        if (await _dbContext.JobRequisitions.AsNoTracking().AnyAsync(
                item => item.RequisitionCode == requisitionCode,
                cancellationToken))
        {
            return Result<JobRequisitionDto>.Failure(UseCaseErrors.JobRequisitionConflict);
        }

        var entity = new JobRequisition
        {
            Id = Guid.NewGuid(),
            RequisitionCode = requisitionCode,
            Title = request.Title.Trim(),
            DepartmentId = request.DepartmentId,
            PositionId = request.PositionId,
            Description = request.Description?.Trim(),
            OpeningsCount = request.OpeningsCount,
            MinimumRelevantExperienceMonths = request.MinimumRelevantExperienceMonths,
            MandatorySkillCoverageThreshold = request.MandatorySkillCoverageThreshold,
            OverallSkillCoverageThreshold = request.OverallSkillCoverageThreshold,
            JobRequisitionStatus = JobRequisitionStatus.Draft,
            OpenedAt = request.OpenedAt,
            ClosedAt = null,
            CreatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime
        };

        await _dbContext.JobRequisitions.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<JobRequisitionDto>.Success(Map(entity, references.Department!, references.Position!, 0));
    }

    public async Task<Result<JobRequisitionDto>> UpdateAsync(
        Guid id,
        UpdateJobRequisitionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _dbContext.JobRequisitions
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result<JobRequisitionDto>.Failure(UseCaseErrors.JobRequisitionNotFound);

        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
            return Result<JobRequisitionDto>.ValidationFailure(validation.Errors);

        if (IsTerminal(entity.JobRequisitionStatus))
            return Result<JobRequisitionDto>.Failure(UseCaseErrors.JobRequisitionLocked);

        if (entity.ClosedAt.HasValue && request.OpenedAt > entity.ClosedAt.Value)
        {
            return Result<JobRequisitionDto>.ValidationFailure(new ValidationError(
                "opened_at_after_closed_at",
                "OpenedAt cannot be after ClosedAt.",
                nameof(request.OpenedAt)));
        }

        var changesEvaluationMeaning = entity.DepartmentId != request.DepartmentId
            || entity.PositionId != request.PositionId;
        if (changesEvaluationMeaning
            && await _dbContext.CandidateEvaluationCases.AsNoTracking().AnyAsync(
                item => item.JobRequisitionId == id,
                cancellationToken))
        {
            return Result<JobRequisitionDto>.Failure(UseCaseErrors.JobRequisitionLocked);
        }

        var references = await ValidateReferencesAsync(
            request.DepartmentId,
            request.PositionId,
            cancellationToken);
        if (references.Error is not null)
            return Result<JobRequisitionDto>.Failure(references.Error);

        var requisitionCode = request.RequisitionCode.Trim();
        if (entity.RequisitionCode != requisitionCode
            && await _dbContext.JobRequisitions.AsNoTracking().AnyAsync(
                item => item.Id != id && item.RequisitionCode == requisitionCode,
                cancellationToken))
        {
            return Result<JobRequisitionDto>.Failure(UseCaseErrors.JobRequisitionConflict);
        }

        entity.RequisitionCode = requisitionCode;
        entity.Title = request.Title.Trim();
        entity.DepartmentId = request.DepartmentId;
        entity.PositionId = request.PositionId;
        entity.Description = request.Description?.Trim();
        entity.OpeningsCount = request.OpeningsCount;
        entity.MinimumRelevantExperienceMonths = request.MinimumRelevantExperienceMonths;
        entity.MandatorySkillCoverageThreshold = request.MandatorySkillCoverageThreshold;
        entity.OverallSkillCoverageThreshold = request.OverallSkillCoverageThreshold;
        entity.OpenedAt = request.OpenedAt;
        entity.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;

        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetDtoByIdAsync(id, cancellationToken);
    }

    public async Task<Result<JobRequisitionDto>> ChangeStatusAsync(
        Guid id,
        ChangeJobRequisitionStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _statusValidator.Validate(request);
        if (!validation.IsValid)
            return Result<JobRequisitionDto>.ValidationFailure(validation.Errors);

        var entity = await _dbContext.JobRequisitions
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result<JobRequisitionDto>.Failure(UseCaseErrors.JobRequisitionNotFound);
        if (entity.JobRequisitionStatus == request.Status)
            return Result<JobRequisitionDto>.Failure(UseCaseErrors.JobRequisitionStatusNoChange);
        if (!CanTransition(entity.JobRequisitionStatus, request.Status))
        {
            return Result<JobRequisitionDto>.Failure(
                UseCaseErrors.JobRequisitionStatusTransitionInvalid);
        }

        if (request.ClosedAt < entity.OpenedAt)
        {
            return Result<JobRequisitionDto>.ValidationFailure(new ValidationError(
                "closed_at_before_opened_at",
                "ClosedAt cannot be before OpenedAt.",
                nameof(request.ClosedAt)));
        }

        entity.JobRequisitionStatus = request.Status;
        entity.ClosedAt = request.ClosedAt;
        entity.UpdatedAtUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetDtoByIdAsync(id, cancellationToken);
    }

    private IQueryable<JobRequisitionDto> ProjectRequisitions(IQueryable<JobRequisition> query) =>
        query.Select(item => new JobRequisitionDto(
                item.Id,
                item.RequisitionCode,
                item.Title,
                item.DepartmentId,
                item.Department.Code,
                item.Department.Name,
                item.PositionId,
                item.Position.Code,
                item.Position.Name,
                item.Description,
                item.OpeningsCount,
                item.MinimumRelevantExperienceMonths,
                item.JobRequisitionStatus,
                item.OpenedAt,
                item.ClosedAt,
                item.MandatorySkillCoverageThreshold,
                item.OverallSkillCoverageThreshold,
                item.CreatedAtUtc,
                item.UpdatedAtUtc,
                item.Requirements.Count));

    private async Task<Result<JobRequisitionDto>> GetDtoByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.JobRequisitions.AsNoTracking().Where(item => item.Id == id);
        var item = await ProjectRequisitions(query).SingleAsync(cancellationToken);
        return Result<JobRequisitionDto>.Success(item);
    }

    private async Task<ReferenceValidation> ValidateReferencesAsync(
        Guid departmentId,
        Guid positionId,
        CancellationToken cancellationToken)
    {
        var department = await _dbContext.Departments.AsNoTracking()
            .Where(item => item.Id == departmentId)
            .Select(item => new ReferenceItem(item.Id, item.Code, item.Name, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        if (department is null)
            return new(UseCaseErrors.DepartmentNotFound, null, null);
        if (!department.IsActive)
            return new(UseCaseErrors.DepartmentInactive, null, null);

        var position = await _dbContext.Positions.AsNoTracking()
            .Where(item => item.Id == positionId)
            .Select(item => new ReferenceItem(item.Id, item.Code, item.Name, item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        if (position is null)
            return new(UseCaseErrors.PositionNotFound, null, null);
        if (!position.IsActive)
            return new(UseCaseErrors.PositionInactive, null, null);
        return new(null, department, position);
    }

    private static bool IsTerminal(JobRequisitionStatus status) =>
        status is JobRequisitionStatus.Closed or JobRequisitionStatus.Cancelled;

    private static bool CanTransition(JobRequisitionStatus current, JobRequisitionStatus next) =>
        current switch
        {
            JobRequisitionStatus.Draft => next is JobRequisitionStatus.Open
                or JobRequisitionStatus.Cancelled,
            JobRequisitionStatus.Open => next is JobRequisitionStatus.OnHold
                or JobRequisitionStatus.Closed
                or JobRequisitionStatus.Cancelled,
            JobRequisitionStatus.OnHold => next is JobRequisitionStatus.Open
                or JobRequisitionStatus.Closed
                or JobRequisitionStatus.Cancelled,
            _ => false
        };

    private static JobRequisitionDto Map(
        JobRequisition entity,
        ReferenceItem department,
        ReferenceItem position,
        int requirementCount) =>
        new(
            entity.Id,
            entity.RequisitionCode,
            entity.Title,
            entity.DepartmentId,
            department.Code,
            department.Name,
            entity.PositionId,
            position.Code,
            position.Name,
            entity.Description,
            entity.OpeningsCount,
            entity.MinimumRelevantExperienceMonths,
            entity.JobRequisitionStatus,
            entity.OpenedAt,
            entity.ClosedAt,
            entity.MandatorySkillCoverageThreshold,
            entity.OverallSkillCoverageThreshold,
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc,
            requirementCount);

    private sealed record ReferenceItem(
        Guid Id,
        string Code,
        string Name,
        bool IsActive);

    private sealed record ReferenceValidation(
        Error? Error,
        ReferenceItem? Department,
        ReferenceItem? Position);
}
