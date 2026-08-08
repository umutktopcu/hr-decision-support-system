using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.SemanticMatching.Retrieval.Models;

namespace HrDecisionSupport.Application.SemanticMatching.Retrieval;

public interface ISemanticRetrievalService
{
    /// <summary>
    /// Retrieves the top N candidates based on semantic similarity to the job document.
    /// Empty documents (job or candidate) will result in a validation failure.
    /// Candidates with duplicate IDs will result in a validation failure.
    /// </summary>
    Task<Result<IReadOnlyList<SemanticRetrievalResult>>> RetrieveTopCandidatesAsync(
        string jobDocument,
        IReadOnlyList<SemanticCandidateDocument> candidates,
        int topN,
        CancellationToken cancellationToken = default);
}
