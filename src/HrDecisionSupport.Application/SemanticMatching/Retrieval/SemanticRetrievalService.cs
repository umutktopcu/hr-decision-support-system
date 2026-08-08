using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.SemanticMatching.Embeddings;
using HrDecisionSupport.Application.SemanticMatching.Retrieval.Models;

namespace HrDecisionSupport.Application.SemanticMatching.Retrieval;

public class SemanticRetrievalService : ISemanticRetrievalService
{
    private readonly IEmbeddingProvider _embeddingProvider;

    public SemanticRetrievalService(IEmbeddingProvider embeddingProvider)
    {
        _embeddingProvider = embeddingProvider;
    }

    public async Task<Result<IReadOnlyList<SemanticRetrievalResult>>> RetrieveTopCandidatesAsync(
        string jobDocument,
        IReadOnlyList<SemanticCandidateDocument> candidates,
        int topN,
        CancellationToken cancellationToken = default)
    {
        if (topN <= 0)
            return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure("Validation", "TopN must be greater than 0.");

        if (string.IsNullOrWhiteSpace(jobDocument))
            return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure("Validation", "Job document cannot be null or whitespace.");

        if (candidates == null || candidates.Count == 0)
            return Result<IReadOnlyList<SemanticRetrievalResult>>.Success(Array.Empty<SemanticRetrievalResult>());

        var candidateIds = new HashSet<Guid>();
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate.Text))
                return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure("Validation", $"Candidate document for {candidate.CandidateId} is empty.");

            if (!candidateIds.Add(candidate.CandidateId))
                return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure("Validation", $"Duplicate candidate ID found: {candidate.CandidateId}");
        }

        // Job document is encoded as a query (model may apply query-specific instructions)
        var queryResult = await _embeddingProvider.GenerateQueryEmbeddingAsync(jobDocument, cancellationToken);
        if (queryResult.IsFailure)
            return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure(queryResult.Error ?? Error.Failure("Unknown", "Query embedding generation failed."));

        // Candidate documents are encoded as documents (no query instruction)
        var candidateTexts = candidates.Select(c => c.Text).ToList().AsReadOnly();
        var documentResult = await _embeddingProvider.GenerateDocumentEmbeddingsAsync(candidateTexts, cancellationToken);
        if (documentResult.IsFailure)
            return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure(documentResult.Error ?? Error.Failure("Unknown", "Document embedding generation failed."));

        var candidateVectors = documentResult.Value;
        if (candidateVectors.Count != candidates.Count)
        {
            return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure("Validation",
                $"Embedding provider returned {candidateVectors.Count} vectors, expected {candidates.Count}.");
        }

        var jobVector = queryResult.Value;
        var results = new List<SemanticRetrievalResult>(candidates.Count);

        for (int i = 0; i < candidates.Count; i++)
        {
            var candidate = candidates[i];
            var candidateVector = candidateVectors[i];

            double score;
            try
            {
                score = CosineSimilarity.Calculate(jobVector, candidateVector);
            }
            catch (ArgumentException ex)
            {
                return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure("Validation", $"Failed to calculate cosine similarity for {candidate.CandidateId}: {ex.Message}");
            }

            results.Add(new SemanticRetrievalResult(candidate.CandidateId, score, candidate.Text));
        }

        var topCandidates = results
            .OrderByDescending(x => x.CosineSimilarityScore)
            .ThenBy(x => x.CandidateId)
            .Take(topN)
            .ToList()
            .AsReadOnly();

        return Result<IReadOnlyList<SemanticRetrievalResult>>.Success(topCandidates);
    }
}
