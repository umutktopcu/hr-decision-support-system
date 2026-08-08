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

        // Batch all documents: [0] is job, [1..N] are candidates
        var texts = new List<string>(candidates.Count + 1) { jobDocument };
        texts.AddRange(candidates.Select(c => c.Text));

        var embeddingsResult = await _embeddingProvider.GenerateEmbeddingsAsync(texts, cancellationToken);
        if (embeddingsResult.IsFailure)
        {
            // Propagate the error
            return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure(embeddingsResult.Error ?? Error.Failure("Unknown", "Embedding generation failed."));
        }

        var embeddings = embeddingsResult.Value;
        if (embeddings.Count != texts.Count)
        {
            return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure("Validation", $"Embedding provider returned {embeddings.Count} vectors, expected {texts.Count}.");
        }

        var jobVector = embeddings[0];
        var candidateVectors = embeddings.Skip(1).ToList();

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
