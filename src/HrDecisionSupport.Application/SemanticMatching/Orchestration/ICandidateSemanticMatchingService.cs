using System;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.SemanticMatching.Orchestration.Models;

namespace HrDecisionSupport.Application.SemanticMatching.Orchestration;

public interface ICandidateSemanticMatchingService
{
    Task<Result<CandidateSemanticMatchingBatchResult>> MatchApplicantsForJobAsync(
        Guid jobRequisitionId,
        int topN,
        CancellationToken cancellationToken = default);
}
