using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.Profiles.Projects;

public interface IPersonProjectService
{
    Task<Result<IReadOnlyList<PersonProjectDto>>> ListByPersonAsync(Guid personId, CancellationToken cancellationToken = default);
    Task<Result<PersonProjectDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<PersonProjectDto>> CreateAsync(CreatePersonProjectRequest request, CancellationToken cancellationToken = default);
    Task<Result<PersonProjectDto>> UpdateAsync(Guid id, UpdatePersonProjectRequest request, CancellationToken cancellationToken = default);
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
