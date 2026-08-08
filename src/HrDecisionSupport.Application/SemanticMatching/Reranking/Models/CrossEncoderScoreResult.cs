using System;

namespace HrDecisionSupport.Application.SemanticMatching.Reranking.Models;

public record CrossEncoderScoreResult(
    Guid CandidateId,
    double RawScore,
    double JobFitScore
);
