using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.Profiles.Sectors;

public interface IPersonSectorExperienceService
{
    Task<Result<IReadOnlyList<PersonSectorExperienceDto>>> ListByPersonAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<Result<PersonSectorExperienceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PersonSectorExperienceDto>> CreateAsync(CreatePersonSectorExperienceRequest request, CancellationToken cancellationToken = default);
    Task<Result<PersonSectorExperienceDto>> UpdateAsync(Guid id, UpdatePersonSectorExperienceRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
