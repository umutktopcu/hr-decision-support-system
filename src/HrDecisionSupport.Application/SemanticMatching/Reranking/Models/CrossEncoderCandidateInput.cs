using System;

namespace HrDecisionSupport.Application.SemanticMatching.Reranking.Models;

public record CrossEncoderCandidateInput(
    Guid CandidateId,
    string CandidateDocumentText
);
