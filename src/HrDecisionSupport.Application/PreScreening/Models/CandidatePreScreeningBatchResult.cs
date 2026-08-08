using System;
using System.Collections.Generic;
using System.Linq;

namespace HrDecisionSupport.Application.PreScreening.Models;

public sealed record CandidatePreScreeningBatchResult(
    Guid JobRequisitionId,
    int TotalApplicants,
    int EligibleCount,
    int RejectedCount,
    IReadOnlyList<CandidatePreScreeningResult> Results)
{
    public IEnumerable<CandidatePreScreeningResult> EligibleResults => Results.Where(x => x.EligibleForSemanticEvaluation);
}
