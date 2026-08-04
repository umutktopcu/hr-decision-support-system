using HrDecisionSupport.Application.Candidates.Dtos;
using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.Candidates;

public interface ICandidateService
{
    Task<Result<IReadOnlyList<CandidateListItemDto>>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<Result<CandidateDetailsDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<CandidateDetailsDto>> CreateAsync(
        CreateCandidateRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CandidateDetailsDto>> UpdateAsync(
        Guid id,
        UpdateCandidateRequest request,
        CancellationToken cancellationToken = default);
}
