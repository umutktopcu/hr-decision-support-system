using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.Profiles.WorkModes;

public interface IPersonWorkModeExperienceService
{
    Task<Result<IReadOnlyList<PersonWorkModeExperienceDto>>> ListByPersonAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<Result<PersonWorkModeExperienceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PersonWorkModeExperienceDto>> CreateAsync(CreatePersonWorkModeExperienceRequest request, CancellationToken cancellationToken = default);
    Task<Result<PersonWorkModeExperienceDto>> UpdateAsync(Guid id, UpdatePersonWorkModeExperienceRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
