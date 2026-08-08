using System;
using System.Collections.Generic;

namespace HrDecisionSupport.Application.SemanticMatching.Reranking.Models;

public record CandidateJobFitRankingBatchResult(
    Guid JobRequisitionId,
    int TotalApplicants,
    int PreScreeningEligibleCount,
    int PreScreeningRejectedCount,
    int RetrievedCandidateCount,
    int CrossEncodedCandidateCount,
    int RequestedRetrievalTopN,
    int RequestedFinalTopN,
    int FinalCandidateCount,
    string JobDocumentText,
    IReadOnlyList<CandidateJobFitRankingResult> Results
);
