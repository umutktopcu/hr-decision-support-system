using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.Requisitions.Requirements;

public interface IJobRequisitionRequirementService
{
    Task<Result<IReadOnlyList<JobRequisitionRequirementDto>>> ListByRequisitionAsync(
        Guid jobRequisitionId,
        CancellationToken cancellationToken = default);

    Task<Result<JobRequisitionRequirementDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<JobRequisitionRequirementDto>> CreateAsync(
        CreateJobRequisitionRequirementRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JobRequisitionRequirementDto>> UpdateAsync(
        Guid id,
        UpdateJobRequisitionRequirementRequest request,
        CancellationToken cancellationToken = default);

    Task<Result> DeleteAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
