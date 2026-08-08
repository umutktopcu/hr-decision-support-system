using System;

namespace HrDecisionSupport.Application.SemanticMatching.Orchestration.Models;

public record CandidateSemanticMatchingResult(
    Guid CandidateId,
    double CosineSimilarityScore,
    string CandidateDocumentText,
    int MandatoryMatchedCount,
    int MandatoryRequiredCount,
    decimal MandatorySkillCoverage,
    int PreferredMatchedCount,
    int PreferredRequiredCount,
    decimal PreferredSkillCoverage
);
