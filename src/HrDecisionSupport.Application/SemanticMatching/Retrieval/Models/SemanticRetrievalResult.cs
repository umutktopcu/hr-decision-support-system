using System;

namespace HrDecisionSupport.Application.SemanticMatching.Retrieval.Models;

public record SemanticRetrievalResult(Guid CandidateId, double CosineSimilarityScore, string CandidateDocumentText);
