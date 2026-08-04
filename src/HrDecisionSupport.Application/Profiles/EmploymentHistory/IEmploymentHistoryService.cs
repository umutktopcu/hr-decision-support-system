using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.Profiles.EmploymentHistory;

public interface IEmploymentHistoryService
{
    Task<Result<IReadOnlyList<EmploymentHistoryDto>>> ListByPersonAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<Result<EmploymentHistoryDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<EmploymentHistoryDto>> CreateAsync(CreateEmploymentHistoryRequest request, CancellationToken cancellationToken = default);
    Task<Result<EmploymentHistoryDto>> UpdateAsync(Guid id, UpdateEmploymentHistoryRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
