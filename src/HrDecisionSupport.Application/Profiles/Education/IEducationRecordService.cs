using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.Profiles.Education;

public interface IEducationRecordService
{
    Task<Result<IReadOnlyList<EducationRecordDto>>> ListByPersonAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<Result<EducationRecordDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<EducationRecordDto>> CreateAsync(CreateEducationRecordRequest request, CancellationToken cancellationToken = default);
    Task<Result<EducationRecordDto>> UpdateAsync(Guid id, UpdateEducationRecordRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
