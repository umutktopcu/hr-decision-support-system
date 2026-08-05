using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.Requisitions;

public interface IJobRequisitionService
{
    Task<Result<IReadOnlyList<JobRequisitionDto>>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<Result<JobRequisitionDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<JobRequisitionDto>> CreateAsync(
        CreateJobRequisitionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JobRequisitionDto>> UpdateAsync(
        Guid id,
        UpdateJobRequisitionRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<JobRequisitionDto>> ChangeStatusAsync(
        Guid id,
        ChangeJobRequisitionStatusRequest request,
        CancellationToken cancellationToken = default);
}
