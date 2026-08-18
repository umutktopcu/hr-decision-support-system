using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.SemanticMatching.Embeddings;
using HrDecisionSupport.Application.SemanticMatching.Retrieval.Models;

namespace HrDecisionSupport.Application.SemanticMatching.Retrieval;

public class SemanticRetrievalService : ISemanticRetrievalService
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly IEmbeddingStore _embeddingStore;
    private readonly string _modelName;
    private readonly int _expectedDimension;
    private readonly TimeProvider _timeProvider;

    public SemanticRetrievalService(
        IEmbeddingProvider embeddingProvider,
        IEmbeddingStore embeddingStore,
        string modelName,
        int expectedDimension,
        TimeProvider timeProvider)
    {
        _embeddingProvider = embeddingProvider;
        _embeddingStore = embeddingStore;
        _modelName = !string.IsNullOrWhiteSpace(modelName)
            ? modelName
            : throw new ArgumentException("Embedding model name is required.", nameof(modelName));
        _expectedDimension = expectedDimension > 0
            ? expectedDimension
            : throw new ArgumentOutOfRangeException(nameof(expectedDimension));
        _timeProvider = timeProvider;
    }

    public async Task<Result<IReadOnlyList<SemanticRetrievalResult>>> RetrieveTopCandidatesAsync(
        Guid jobRequisitionId,
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

        var jobHash = ComputeDocumentHash(jobDocument);
        var storedJobEmbedding = await _embeddingStore.GetJobRequisitionEmbeddingAsync(
            jobRequisitionId,
            cancellationToken);

        float[] jobVector;
        if (IsValid(storedJobEmbedding, jobRequisitionId, jobHash))
        {
            jobVector = storedJobEmbedding!.Vector;
        }
        else
        {
            // Job document remains encoded as a query so model input semantics stay unchanged.
            var queryResult = await _embeddingProvider.GenerateQueryEmbeddingAsync(
                jobDocument,
                cancellationToken);
            if (queryResult.IsFailure)
            {
                return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure(
                    queryResult.Error ?? Error.Failure("Unknown", "Query embedding generation failed."));
            }

            jobVector = queryResult.Value;
            if (!IsValidVector(jobVector))
                return InvalidGeneratedVector("Job", jobVector);

            await _embeddingStore.UpsertJobRequisitionEmbeddingAsync(
                CreateStoredEmbedding(jobRequisitionId, jobHash, jobVector),
                cancellationToken);
        }

        var candidateHashes = candidates.ToDictionary(
            candidate => candidate.CandidateId,
            candidate => ComputeDocumentHash(candidate.Text));
        var storedCandidateEmbeddings = await _embeddingStore.GetCandidateEmbeddingsAsync(
            candidateIds,
            cancellationToken);

        var candidateVectors = new float[candidates.Count][];
        var missingIndices = new List<int>();
        var missingTexts = new List<string>();

        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            if (storedCandidateEmbeddings.TryGetValue(candidate.CandidateId, out var stored) &&
                IsValid(stored, candidate.CandidateId, candidateHashes[candidate.CandidateId]))
            {
                candidateVectors[index] = stored.Vector;
            }
            else
            {
                missingIndices.Add(index);
                missingTexts.Add(candidate.Text);
            }
        }

        if (missingTexts.Count > 0)
        {
            // Only persistent-cache misses are sent as documents, in their original relative order.
            var documentResult = await _embeddingProvider.GenerateDocumentEmbeddingsAsync(
                missingTexts.AsReadOnly(),
                cancellationToken);
            if (documentResult.IsFailure)
            {
                return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure(
                    documentResult.Error ?? Error.Failure("Unknown", "Document embedding generation failed."));
            }

            if (documentResult.Value.Count != missingTexts.Count)
            {
                return Result<IReadOnlyList<SemanticRetrievalResult>>.Failure(
                    "Validation",
                    $"Embedding provider returned {documentResult.Value.Count} vectors, expected {missingTexts.Count}.");
            }

            var generatedEmbeddings = new List<StoredEmbedding>(missingTexts.Count);
            for (var missIndex = 0; missIndex < missingIndices.Count; missIndex++)
            {
                var originalIndex = missingIndices[missIndex];
                var vector = documentResult.Value[missIndex];
                if (!IsValidVector(vector))
                    return InvalidGeneratedVector($"Candidate {candidates[originalIndex].CandidateId}", vector);

                candidateVectors[originalIndex] = vector;
                var candidate = candidates[originalIndex];
                generatedEmbeddings.Add(CreateStoredEmbedding(
                    candidate.CandidateId,
                    candidateHashes[candidate.CandidateId],
                    vector));
            }

            await _embeddingStore.UpsertCandidateEmbeddingsAsync(
                generatedEmbeddings.AsReadOnly(),
                cancellationToken);
        }

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

    private StoredEmbedding CreateStoredEmbedding(Guid entityId, string documentHash, float[] vector) =>
        new(
            entityId,
            documentHash,
            _modelName,
            vector,
            _timeProvider.GetUtcNow().UtcDateTime);

    private bool IsValid(StoredEmbedding? embedding, Guid entityId, string documentHash) =>
        embedding is not null &&
        embedding.EntityId == entityId &&
        string.Equals(embedding.DocumentHash, documentHash, StringComparison.Ordinal) &&
        string.Equals(embedding.ModelName, _modelName, StringComparison.Ordinal) &&
        IsValidVector(embedding.Vector);

    private bool IsValidVector(float[]? vector) =>
        vector is { Length: var length } &&
        length == _expectedDimension &&
        vector.All(float.IsFinite);

    private static string ComputeDocumentHash(string document) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(document))).ToLowerInvariant();

    private Result<IReadOnlyList<SemanticRetrievalResult>> InvalidGeneratedVector(
        string subject,
        float[]? vector) =>
        Result<IReadOnlyList<SemanticRetrievalResult>>.Failure(
            "Validation",
            $"{subject} embedding has dimension {vector?.Length ?? 0} or contains a non-finite value; expected {_expectedDimension} finite values.");
}
