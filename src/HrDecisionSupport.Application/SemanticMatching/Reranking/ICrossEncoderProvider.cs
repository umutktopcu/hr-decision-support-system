using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.SemanticMatching.Reranking.Models;

namespace HrDecisionSupport.Application.SemanticMatching.Reranking;

public interface ICrossEncoderProvider
{
    Task<Result<IReadOnlyList<CrossEncoderScoreResult>>> ScoreAsync(
        string jobDocument,
        IReadOnlyList<CrossEncoderCandidateInput> candidates,
        CancellationToken cancellationToken = default);
}
