using System;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.SemanticMatching.Reranking.Models;

namespace HrDecisionSupport.Application.SemanticMatching.Reranking;

public interface ICandidateJobFitRankingService
{
    Task<Result<CandidateJobFitRankingBatchResult>> RankApplicantsForJobAsync(
        Guid jobRequisitionId,
        int retrievalTopN,
        int finalTopN,
        CancellationToken cancellationToken = default);
}
