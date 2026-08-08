using System;
using System.Collections.Generic;
using HrDecisionSupport.Application.PreScreening.Models;
using HrDecisionSupport.Application.SemanticMatching.Retrieval.Models;

namespace HrDecisionSupport.Application.SemanticMatching.Orchestration.Models;

public record CandidateSemanticMatchingBatchResult(
    Guid JobRequisitionId,
    int TotalApplicants,
    int PreScreeningEligibleCount,
    int PreScreeningRejectedCount,
    int RequestedTopN,
    int RetrievedCount,
    string JobDocumentText,
    IReadOnlyList<CandidatePreScreeningResult> PreScreeningResults,
    IReadOnlyList<CandidateSemanticMatchingResult> Results
);
