using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.CandidateEvaluations;

public interface ICandidateEvaluationCaseService
{
    Task<Result<IReadOnlyList<CandidateEvaluationCaseDto>>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CandidateEvaluationCaseDto>>> ListByRequisitionAsync(
        Guid jobRequisitionId,
        CancellationToken cancellationToken = default);

    Task<Result<IReadOnlyList<CandidateEvaluationCaseDto>>> ListByCandidateAsync(
        Guid candidateId,
        CancellationToken cancellationToken = default);

    Task<Result<CandidateEvaluationCaseDetailDto>> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<Result<CandidateEvaluationCaseDto>> CreateAsync(
        CreateCandidateEvaluationCaseRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CandidateEvaluationCaseDto>> UpdateAsync(
        Guid id,
        UpdateCandidateEvaluationCaseRequest request,
        CancellationToken cancellationToken = default);

    Task<Result<CandidateEvaluationCaseDto>> ChangeStatusAsync(
        Guid id,
        ChangeCandidateEvaluationCaseStatusRequest request,
        CancellationToken cancellationToken = default);
}
