using System;
using System.Collections.Generic;
using HrDecisionSupport.Application.PreScreening.Models;

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
    IReadOnlyList<CandidatePreScreeningResult> PreScreeningResults,
    IReadOnlyList<CandidateJobFitRankingResult> Results
);
