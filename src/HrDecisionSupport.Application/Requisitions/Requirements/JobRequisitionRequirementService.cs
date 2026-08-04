using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.Requisitions.Requirements;

public sealed class JobRequisitionRequirementService : IJobRequisitionRequirementService
{
    private readonly IHrDecisionSupportDbContext _dbContext;
    private readonly IValidator<CreateJobRequisitionRequirementRequest> _createValidator;
    private readonly IValidator<UpdateJobRequisitionRequirementRequest> _updateValidator;

    public JobRequisitionRequirementService(
        IHrDecisionSupportDbContext dbContext,
        IValidator<CreateJobRequisitionRequirementRequest> createValidator,
        IValidator<UpdateJobRequisitionRequirementRequest> updateValidator)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updateValidator = updateValidator ?? throw new ArgumentNullException(nameof(updateValidator));
    }

    public async Task<Result<IReadOnlyList<JobRequisitionRequirementDto>>> ListByRequisitionAsync(
        Guid jobRequisitionId,
        CancellationToken cancellationToken = default)
    {
        if (jobRequisitionId == Guid.Empty)
        {
            return Result<IReadOnlyList<JobRequisitionRequirementDto>>.ValidationFailure(
                new ValidationError(
                    "job_requisition_id_required",
                    "JobRequisitionId must not be empty.",
                    "JobRequisitionId"));
        }

        if (!await _dbContext.JobRequisitions.AsNoTracking().AnyAsync(
                item => item.Id == jobRequisitionId,
                cancellationToken))
        {
            return Result<IReadOnlyList<JobRequisitionRequirementDto>>.Failure(
                UseCaseErrors.JobRequisitionNotFound);
        }

        var items = await ProjectRequirements()
            .Where(item => item.JobRequisitionId == jobRequisitionId)
            .OrderByDescending(item => item.IsRequired)
            .ThenBy(item => item.CompetencyName)
            .ThenBy(item => item.Id)
            .ToListAsync(cancellationToken);
        return Result<IReadOnlyList<JobRequisitionRequirementDto>>.Success(items);
    }

    public async Task<Result<JobRequisitionRequirementDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var item = await ProjectRequirements()
            .SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        return item is null
            ? Result<JobRequisitionRequirementDto>.Failure(
                UseCaseErrors.JobRequisitionRequirementNotFound)
            : Result<JobRequisitionRequirementDto>.Success(item);
    }

    public async Task<Result<JobRequisitionRequirementDto>> CreateAsync(
        CreateJobRequisitionRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var validation = _createValidator.Validate(request);
        if (!validation.IsValid)
            return Result<JobRequisitionRequirementDto>.ValidationFailure(validation.Errors);

        var requisitionStatus = await _dbContext.JobRequisitions.AsNoTracking()
            .Where(item => item.Id == request.JobRequisitionId)
            .Select(item => (JobRequisitionStatus?)item.JobRequisitionStatus)
            .SingleOrDefaultAsync(cancellationToken);
        if (!requisitionStatus.HasValue)
        {
            return Result<JobRequisitionRequirementDto>.Failure(
                UseCaseErrors.JobRequisitionNotFound);
        }
        if (IsTerminal(requisitionStatus.Value))
            return Result<JobRequisitionRequirementDto>.Failure(UseCaseErrors.JobRequisitionLocked);

        var competency = await _dbContext.Competencies.AsNoTracking()
            .Where(item => item.Id == request.CompetencyId)
            .Select(item => new CompetencyItem(
                item.Id,
                item.Code,
                item.Name,
                item.CompetencyCategory,
                item.IsActive))
            .SingleOrDefaultAsync(cancellationToken);
        if (competency is null)
        {
            return Result<JobRequisitionRequirementDto>.Failure(
                UseCaseErrors.CompetencyNotFound);
        }
        if (!competency.IsActive)
            return Result<JobRequisitionRequirementDto>.Failure(UseCaseErrors.CompetencyInactive);

        if (await _dbContext.JobRequisitionRequirements.AsNoTracking().AnyAsync(
                item => item.JobRequisitionId == request.JobRequisitionId
                    && item.CompetencyId == request.CompetencyId,
                cancellationToken))
        {
            return Result<JobRequisitionRequirementDto>.Failure(
                UseCaseErrors.JobRequisitionRequirementConflict);
        }

        var entity = new JobRequisitionRequirement
        {
            Id = Guid.NewGuid(),
            JobRequisitionId = request.JobRequisitionId,
            CompetencyId = request.CompetencyId,
            MinimumExperienceMonths = request.MinimumExperienceMonths,
            MinimumProficiencyLevel = request.MinimumProficiencyLevel,
            IsRequired = request.IsRequired,
            Notes = request.Notes?.Trim()
        };
        await _dbContext.JobRequisitionRequirements.AddAsync(entity, cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result<JobRequisitionRequirementDto>.Success(Map(entity, competency));
    }

    public async Task<Result<JobRequisitionRequirementDto>> UpdateAsync(
        Guid id,
        UpdateJobRequisitionRequirementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var entity = await _dbContext.JobRequisitionRequirements
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
        {
            return Result<JobRequisitionRequirementDto>.Failure(
                UseCaseErrors.JobRequisitionRequirementNotFound);
        }

        var validation = _updateValidator.Validate(request);
        if (!validation.IsValid)
            return Result<JobRequisitionRequirementDto>.ValidationFailure(validation.Errors);

        if (await IsRequisitionTerminalAsync(entity.JobRequisitionId, cancellationToken))
            return Result<JobRequisitionRequirementDto>.Failure(UseCaseErrors.JobRequisitionLocked);

        entity.MinimumExperienceMonths = request.MinimumExperienceMonths;
        entity.MinimumProficiencyLevel = request.MinimumProficiencyLevel;
        entity.IsRequired = request.IsRequired;
        entity.Notes = request.Notes?.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task<Result> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await _dbContext.JobRequisitionRequirements
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null)
            return Result.Failure(UseCaseErrors.JobRequisitionRequirementNotFound);
        if (await IsRequisitionTerminalAsync(entity.JobRequisitionId, cancellationToken))
            return Result.Failure(UseCaseErrors.JobRequisitionLocked);

        _dbContext.JobRequisitionRequirements.Remove(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private IQueryable<JobRequisitionRequirementDto> ProjectRequirements() =>
        _dbContext.JobRequisitionRequirements
            .AsNoTracking()
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
                item.Notes));

    private async Task<bool> IsRequisitionTerminalAsync(
        Guid requisitionId,
        CancellationToken cancellationToken) =>
        await _dbContext.JobRequisitions.AsNoTracking().AnyAsync(
            item => item.Id == requisitionId
                && (item.JobRequisitionStatus == JobRequisitionStatus.Closed
                    || item.JobRequisitionStatus == JobRequisitionStatus.Cancelled),
            cancellationToken);

    private static bool IsTerminal(JobRequisitionStatus status) =>
        status is JobRequisitionStatus.Closed or JobRequisitionStatus.Cancelled;

    private static JobRequisitionRequirementDto Map(
        JobRequisitionRequirement entity,
        CompetencyItem competency) =>
        new(
            entity.Id,
            entity.JobRequisitionId,
            entity.CompetencyId,
            competency.Code,
            competency.Name,
            competency.CompetencyCategory,
            entity.MinimumExperienceMonths,
            entity.MinimumProficiencyLevel,
            entity.IsRequired,
            entity.Notes);

    private sealed record CompetencyItem(
        Guid Id,
        string Code,
        string Name,
        CompetencyCategory CompetencyCategory,
        bool IsActive);
}
