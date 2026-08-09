using System;

namespace HrDecisionSupport.Application.SemanticMatching.Reranking.Models;

public record CandidateJobFitRankingResult(
    Guid CandidateId,
    int FinalRank,
    int SkillTier,
    int EmbeddingRank,
    double CosineSimilarityScore,
    double CrossEncoderRawScore,
    double JobFitScore,
    string CandidateDocumentText,
    int MandatoryMatchedCount,
    int MandatoryRequiredCount,
    decimal MandatorySkillCoverage,
    int PreferredMatchedCount,
    int PreferredRequiredCount,
    decimal PreferredSkillCoverage
);
