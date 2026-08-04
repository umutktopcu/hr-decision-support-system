using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.Profiles.Languages;

public interface IPersonLanguageService
{
    Task<Result<IReadOnlyList<PersonLanguageDto>>> ListByPersonAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<Result<PersonLanguageDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PersonLanguageDto>> CreateAsync(CreatePersonLanguageRequest request, CancellationToken cancellationToken = default);
    Task<Result<PersonLanguageDto>> UpdateAsync(Guid id, UpdatePersonLanguageRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
