using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.Profiles.Competencies;

public interface IPersonCompetencyService
{
    Task<Result<IReadOnlyList<PersonCompetencyDto>>> ListByPersonAsync(
        Guid personId, CancellationToken cancellationToken = default);
    Task<Result<PersonCompetencyDto>> GetByIdAsync(
        Guid id, CancellationToken cancellationToken = default);
    Task<Result<PersonCompetencyDto>> CreateAsync(
        CreatePersonCompetencyRequest request, CancellationToken cancellationToken = default);
    Task<Result<PersonCompetencyDto>> UpdateAsync(
        Guid id, UpdatePersonCompetencyRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
