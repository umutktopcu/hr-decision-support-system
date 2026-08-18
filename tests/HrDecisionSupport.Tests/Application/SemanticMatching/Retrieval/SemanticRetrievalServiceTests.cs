using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.SemanticMatching.Embeddings;
using HrDecisionSupport.Application.SemanticMatching.Retrieval;
using HrDecisionSupport.Application.SemanticMatching.Retrieval.Models;
using Xunit;

namespace HrDecisionSupport.Tests.Application.SemanticMatching.Retrieval;

public class SemanticRetrievalServiceTests
{
    private const string ModelName = "Qwen/Qwen3-Embedding-0.6B";
    private static readonly Guid JobId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static SemanticRetrievalService CreateService(
        IEmbeddingProvider provider,
        FakeEmbeddingStore? store = null,
        int expectedDimension = 2) =>
        new(provider, store ?? new FakeEmbeddingStore(), ModelName, expectedDimension, TimeProvider.System);

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();

    private static StoredEmbedding Stored(
        Guid entityId,
        string text,
        float[] vector,
        string modelName = ModelName) =>
        new(entityId, Hash(text), modelName, vector, DateTime.UnixEpoch);

    private class FakeEmbeddingProvider : IEmbeddingProvider
    {
        public Dictionary<string, float[]> QueryMappings { get; } = new();
        public Dictionary<string, float[]> DocumentMappings { get; } = new();
        public bool FailQueryCall { get; set; }
        public bool FailDocumentCall { get; set; }
        public bool ReturnWrongDocumentCount { get; set; }
        public List<string> QueryCallTexts { get; } = new();
        public List<string> DocumentCallTexts { get; } = new();
        public List<IReadOnlyList<string>> DocumentCalls { get; } = new();

        public Task<Result<float[]>> GenerateQueryEmbeddingAsync(string queryText, CancellationToken cancellationToken = default)
        {
            QueryCallTexts.Add(queryText);

            if (FailQueryCall)
                return Task.FromResult(Result<float[]>.Failure("Provider", "Query embedding failed"));

            if (QueryMappings.TryGetValue(queryText, out var vec))
                return Task.FromResult(Result<float[]>.Success(vec));

            return Task.FromResult(Result<float[]>.Success(new float[] { 1f, 0f })); // default
        }

        public Task<Result<IReadOnlyList<float[]>>> GenerateDocumentEmbeddingsAsync(IReadOnlyList<string> documentTexts, CancellationToken cancellationToken = default)
        {
            DocumentCalls.Add(documentTexts.ToArray());
            DocumentCallTexts.AddRange(documentTexts);

            if (FailDocumentCall)
                return Task.FromResult(Result<IReadOnlyList<float[]>>.Failure("Provider", "Document embedding failed"));

            if (ReturnWrongDocumentCount)
                return Task.FromResult(Result<IReadOnlyList<float[]>>.Success(new List<float[]> { new float[] { 1f } }));

            var results = new List<float[]>();
            foreach (var text in documentTexts)
            {
                if (DocumentMappings.TryGetValue(text, out var vec))
                    results.Add(vec);
                else
                    results.Add(new float[] { 0.1f, 0.1f }); // default fallback (non-zero)
            }

            return Task.FromResult(Result<IReadOnlyList<float[]>>.Success(results));
        }
    }

    private sealed class FakeEmbeddingStore : IEmbeddingStore
    {
        public Dictionary<Guid, StoredEmbedding> CandidateEmbeddings { get; } = new();
        public Dictionary<Guid, StoredEmbedding> JobEmbeddings { get; } = new();
        public List<IReadOnlyCollection<Guid>> CandidateLookupCalls { get; } = new();
        public List<IReadOnlyCollection<StoredEmbedding>> CandidateUpsertCalls { get; } = new();
        public List<Guid> JobLookupCalls { get; } = new();
        public List<StoredEmbedding> JobUpsertCalls { get; } = new();

        public Task<IReadOnlyDictionary<Guid, StoredEmbedding>> GetCandidateEmbeddingsAsync(
            IReadOnlyCollection<Guid> candidateIds,
            CancellationToken cancellationToken = default)
        {
            CandidateLookupCalls.Add(candidateIds.ToArray());
            IReadOnlyDictionary<Guid, StoredEmbedding> result = candidateIds
                .Where(CandidateEmbeddings.ContainsKey)
                .ToDictionary(id => id, id => CandidateEmbeddings[id]);
            return Task.FromResult(result);
        }

        public Task<StoredEmbedding?> GetJobRequisitionEmbeddingAsync(
            Guid jobRequisitionId,
            CancellationToken cancellationToken = default)
        {
            JobLookupCalls.Add(jobRequisitionId);
            JobEmbeddings.TryGetValue(jobRequisitionId, out var result);
            return Task.FromResult(result);
        }

        public Task UpsertCandidateEmbeddingsAsync(
            IReadOnlyCollection<StoredEmbedding> embeddings,
            CancellationToken cancellationToken = default)
        {
            var snapshot = embeddings.ToArray();
            CandidateUpsertCalls.Add(snapshot);
            foreach (var embedding in snapshot)
                CandidateEmbeddings[embedding.EntityId] = embedding;
            return Task.CompletedTask;
        }

        public Task UpsertJobRequisitionEmbeddingAsync(
            StoredEmbedding embedding,
            CancellationToken cancellationToken = default)
        {
            JobUpsertCalls.Add(embedding);
            JobEmbeddings[embedding.EntityId] = embedding;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_SortsDescendingByCosineScore()
    {
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["Job"] = new float[] { 1f, 0f };
        provider.DocumentMappings["CandidateA"] = new float[] { 0.8f, 0.2f }; // dot=0.8, magB=~0.824, cos > 0
        provider.DocumentMappings["CandidateB"] = new float[] { 1f, 0f };     // Identical to Job -> cos=1
        provider.DocumentMappings["CandidateC"] = new float[] { 0f, 1f };     // Orthogonal to Job -> cos=0

        var sut = CreateService(provider);
        var candidates = new List<SemanticCandidateDocument>
        {
            new(Guid.NewGuid(), "CandidateA"),
            new(Guid.NewGuid(), "CandidateB"),
            new(Guid.NewGuid(), "CandidateC")
        };

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 3);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);

        // Expected order: B (1.0), A (~0.97), C (0.0)
        Assert.Equal("CandidateB", result.Value[0].CandidateDocumentText);
        Assert.Equal(1.0, result.Value[0].CosineSimilarityScore, 5);
        Assert.Equal("CandidateA", result.Value[1].CandidateDocumentText);
        Assert.Equal("CandidateC", result.Value[2].CandidateDocumentText);
        Assert.Equal(0.0, result.Value[2].CosineSimilarityScore, 5);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_JobDocumentUsesQueryEmbeddingMethod()
    {
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["JobText"] = new float[] { 1f, 0f };
        provider.DocumentMappings["CandidateDoc"] = new float[] { 1f, 0f };

        var sut = CreateService(provider);
        var candidates = new List<SemanticCandidateDocument>
        {
            new(Guid.NewGuid(), "CandidateDoc")
        };

        await sut.RetrieveTopCandidatesAsync(JobId, "JobText", candidates, 10);

        // Job document must be sent via query method
        Assert.Contains("JobText", provider.QueryCallTexts);
        Assert.DoesNotContain("JobText", provider.DocumentCallTexts);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_CandidatesUseDocumentEmbeddingMethod()
    {
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["Job"] = new float[] { 1f, 0f };
        provider.DocumentMappings["C1"] = new float[] { 1f, 0f };
        provider.DocumentMappings["C2"] = new float[] { 0f, 1f };

        var sut = CreateService(provider);
        var candidates = new List<SemanticCandidateDocument>
        {
            new(Guid.NewGuid(), "C1"),
            new(Guid.NewGuid(), "C2")
        };

        await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 10);

        // Candidates must be sent via document method, NOT query method
        Assert.Contains("C1", provider.DocumentCallTexts);
        Assert.Contains("C2", provider.DocumentCallTexts);
        Assert.DoesNotContain("C1", provider.QueryCallTexts);
        Assert.DoesNotContain("C2", provider.QueryCallTexts);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_AppliesTopN_Correctly()
    {
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["Job"] = new float[] { 1f, 0f };
        provider.DocumentMappings["C1"] = new float[] { 1f, 0f };
        provider.DocumentMappings["C2"] = new float[] { 1f, 0f };
        provider.DocumentMappings["C3"] = new float[] { 1f, 0f };

        var sut = CreateService(provider);
        var candidates = new List<SemanticCandidateDocument>
        {
            new(Guid.NewGuid(), "C1"),
            new(Guid.NewGuid(), "C2"),
            new(Guid.NewGuid(), "C3")
        };

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_TopNGreaterThanPool_ReturnsAll()
    {
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["Job"] = new float[] { 1f, 0f };
        provider.DocumentMappings["C1"] = new float[] { 1f, 0f };

        var sut = CreateService(provider);
        var candidates = new List<SemanticCandidateDocument>
        {
            new(Guid.NewGuid(), "C1")
        };

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 50);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_InvalidTopN_ReturnsFailure()
    {
        var sut = CreateService(new FakeEmbeddingProvider());

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", new List<SemanticCandidateDocument>(), 0);
        Assert.True(result.IsFailure);

        var result2 = await sut.RetrieveTopCandidatesAsync(JobId, "Job", new List<SemanticCandidateDocument>(), -1);
        Assert.True(result2.IsFailure);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_TieScores_DeterministicByCandidateId()
    {
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["Job"] = new float[] { 1f, 0f };
        provider.DocumentMappings["C1"] = new float[] { 1f, 0f };
        provider.DocumentMappings["C2"] = new float[] { 1f, 0f };

        var id1 = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var id2 = Guid.Parse("22222222-2222-2222-2222-222222222222");

        var sut = CreateService(provider);

        // Pass them in reverse order to ensure sorting logic works
        var candidates = new List<SemanticCandidateDocument>
        {
            new(id2, "C2"),
            new(id1, "C1")
        };

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(id1, result.Value[0].CandidateId); // Secondary tie break ASC by Guid
        Assert.Equal(id2, result.Value[1].CandidateId);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_EmptyPool_ReturnsEmptyWithoutCallingProvider()
    {
        var provider = new FakeEmbeddingProvider { FailQueryCall = true, FailDocumentCall = true }; // Should not be called
        var sut = CreateService(provider);

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", new List<SemanticCandidateDocument>(), 10);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Empty(provider.QueryCallTexts); // Provider NOT called for empty pool
        Assert.Empty(provider.DocumentCallTexts);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_DuplicateCandidateId_ReturnsFailure()
    {
        var id = Guid.NewGuid();
        var candidates = new List<SemanticCandidateDocument>
        {
            new(id, "C1"),
            new(id, "C2")
        };

        var sut = CreateService(new FakeEmbeddingProvider());

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 10);

        Assert.True(result.IsFailure);
        Assert.Contains("Duplicate candidate ID", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_EmptyJobDocument_ReturnsFailure()
    {
        var sut = CreateService(new FakeEmbeddingProvider());

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "   ", new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") }, 10);

        Assert.True(result.IsFailure);
        Assert.Contains("Job document cannot be null or whitespace", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_EmptyCandidateDocument_ReturnsFailure()
    {
        var sut = CreateService(new FakeEmbeddingProvider());
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "") };

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 10);

        Assert.True(result.IsFailure);
        Assert.Contains("empty", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_ProviderReturnsWrongCount_Detected()
    {
        var provider = new FakeEmbeddingProvider { ReturnWrongDocumentCount = true };
        var sut = CreateService(provider);
        // Send 2 candidates but FakeEmbeddingProvider returns only 1 vector → count mismatch
        var candidates = new List<SemanticCandidateDocument>
        {
            new(Guid.NewGuid(), "C1"),
            new(Guid.NewGuid(), "C2")
        };

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 10);

        Assert.True(result.IsFailure);
        Assert.Contains("expected 2", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_QueryProviderFails_PropagatesError()
    {
        var provider = new FakeEmbeddingProvider { FailQueryCall = true };
        var sut = CreateService(provider);
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") };

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("Query embedding failed", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_DocumentProviderFails_PropagatesError()
    {
        var provider = new FakeEmbeddingProvider { FailDocumentCall = true };
        var sut = CreateService(provider);
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") };

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("Document embedding failed", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_DimensionMismatch_ReturnsFailure()
    {
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["Job"] = new float[] { 1f, 0f };
        provider.DocumentMappings["C1"] = new float[] { 1f, 0f, 0f }; // Mismatched dimension

        var sut = CreateService(provider);
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") };

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 10);

        Assert.True(result.IsFailure);
        Assert.Contains("embedding has dimension 3", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_CandidateMiss_CallsProviderAndPersistsVector()
    {
        var candidateId = Guid.NewGuid();
        var provider = new FakeEmbeddingProvider();
        provider.DocumentMappings["Candidate"] = new[] { 0.8f, 0.2f };
        var store = new FakeEmbeddingStore();
        store.JobEmbeddings[JobId] = Stored(JobId, "Job", new[] { 1f, 0f });
        var sut = CreateService(provider, store);

        var result = await sut.RetrieveTopCandidatesAsync(
            JobId,
            "Job",
            new[] { new SemanticCandidateDocument(candidateId, "Candidate") },
            1);

        Assert.True(result.IsSuccess);
        Assert.Single(provider.DocumentCalls);
        Assert.Equal(new[] { "Candidate" }, provider.DocumentCalls[0]);
        var persisted = Assert.Single(Assert.Single(store.CandidateUpsertCalls));
        Assert.Equal(candidateId, persisted.EntityId);
        Assert.Equal(Hash("Candidate"), persisted.DocumentHash);
        Assert.Equal(ModelName, persisted.ModelName);
        Assert.Equal(new[] { 0.8f, 0.2f }, persisted.Vector);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_CandidateHit_SkipsDocumentProvider()
    {
        var candidateId = Guid.NewGuid();
        var provider = new FakeEmbeddingProvider { FailDocumentCall = true, FailQueryCall = true };
        var store = new FakeEmbeddingStore();
        store.JobEmbeddings[JobId] = Stored(JobId, "Job", new[] { 1f, 0f });
        store.CandidateEmbeddings[candidateId] = Stored(candidateId, "Candidate", new[] { 1f, 0f });
        var sut = CreateService(provider, store);

        var result = await sut.RetrieveTopCandidatesAsync(
            JobId,
            "Job",
            new[] { new SemanticCandidateDocument(candidateId, "Candidate") },
            1);

        Assert.True(result.IsSuccess);
        Assert.Empty(provider.QueryCallTexts);
        Assert.Empty(provider.DocumentCalls);
        Assert.Empty(store.CandidateUpsertCalls);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_JobMiss_CallsQueryProviderAndPersistsVector()
    {
        var candidateId = Guid.NewGuid();
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["Job"] = new[] { 1f, 0f };
        var store = new FakeEmbeddingStore();
        store.CandidateEmbeddings[candidateId] = Stored(candidateId, "Candidate", new[] { 1f, 0f });
        var sut = CreateService(provider, store);

        var result = await sut.RetrieveTopCandidatesAsync(
            JobId,
            "Job",
            new[] { new SemanticCandidateDocument(candidateId, "Candidate") },
            1);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Job" }, provider.QueryCallTexts);
        var persisted = Assert.Single(store.JobUpsertCalls);
        Assert.Equal(JobId, persisted.EntityId);
        Assert.Equal(Hash("Job"), persisted.DocumentHash);
        Assert.Equal(ModelName, persisted.ModelName);
        Assert.Equal(new[] { 1f, 0f }, persisted.Vector);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_JobHit_SkipsQueryProvider()
    {
        var candidateId = Guid.NewGuid();
        var provider = new FakeEmbeddingProvider { FailQueryCall = true, FailDocumentCall = true };
        var store = new FakeEmbeddingStore();
        store.JobEmbeddings[JobId] = Stored(JobId, "Job", new[] { 1f, 0f });
        store.CandidateEmbeddings[candidateId] = Stored(candidateId, "Candidate", new[] { 1f, 0f });
        var sut = CreateService(provider, store);

        var result = await sut.RetrieveTopCandidatesAsync(
            JobId,
            "Job",
            new[] { new SemanticCandidateDocument(candidateId, "Candidate") },
            1);

        Assert.True(result.IsSuccess);
        Assert.Empty(provider.QueryCallTexts);
        Assert.Empty(store.JobUpsertCalls);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_ChangedCandidateDocument_RegeneratesOnlyCandidate()
    {
        var candidateId = Guid.NewGuid();
        var provider = new FakeEmbeddingProvider();
        provider.DocumentMappings["Current candidate"] = new[] { 0f, 1f };
        var store = new FakeEmbeddingStore();
        store.JobEmbeddings[JobId] = Stored(JobId, "Job", new[] { 1f, 0f });
        store.CandidateEmbeddings[candidateId] = Stored(candidateId, "Old candidate", new[] { 1f, 0f });
        var sut = CreateService(provider, store);

        var result = await sut.RetrieveTopCandidatesAsync(
            JobId,
            "Job",
            new[] { new SemanticCandidateDocument(candidateId, "Current candidate") },
            1);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Current candidate" }, Assert.Single(provider.DocumentCalls));
        Assert.Equal(Hash("Current candidate"), store.CandidateEmbeddings[candidateId].DocumentHash);
        Assert.Equal(new[] { 0f, 1f }, store.CandidateEmbeddings[candidateId].Vector);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_ChangedJobDocument_RegeneratesOnlyJob()
    {
        var candidateId = Guid.NewGuid();
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["Current job"] = new[] { 0f, 1f };
        var store = new FakeEmbeddingStore();
        store.JobEmbeddings[JobId] = Stored(JobId, "Old job", new[] { 1f, 0f });
        store.CandidateEmbeddings[candidateId] = Stored(candidateId, "Candidate", new[] { 0f, 1f });
        var sut = CreateService(provider, store);

        var result = await sut.RetrieveTopCandidatesAsync(
            JobId,
            "Current job",
            new[] { new SemanticCandidateDocument(candidateId, "Candidate") },
            1);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "Current job" }, provider.QueryCallTexts);
        Assert.Empty(provider.DocumentCalls);
        Assert.Equal(Hash("Current job"), store.JobEmbeddings[JobId].DocumentHash);
        Assert.Equal(new[] { 0f, 1f }, store.JobEmbeddings[JobId].Vector);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_ModelChanged_RegeneratesStoredJobAndCandidate()
    {
        var candidateId = Guid.NewGuid();
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["Job"] = new[] { 1f, 0f };
        provider.DocumentMappings["Candidate"] = new[] { 0f, 1f };
        var store = new FakeEmbeddingStore();
        store.JobEmbeddings[JobId] = Stored(JobId, "Job", new[] { 0.5f, 0.5f }, "old-model");
        store.CandidateEmbeddings[candidateId] = Stored(candidateId, "Candidate", new[] { 0.5f, 0.5f }, "old-model");
        var sut = CreateService(provider, store);

        var result = await sut.RetrieveTopCandidatesAsync(
            JobId,
            "Job",
            new[] { new SemanticCandidateDocument(candidateId, "Candidate") },
            1);

        Assert.True(result.IsSuccess);
        Assert.Single(provider.QueryCallTexts);
        Assert.Equal(new[] { "Candidate" }, Assert.Single(provider.DocumentCalls));
        Assert.Equal(ModelName, store.JobEmbeddings[JobId].ModelName);
        Assert.Equal(ModelName, store.CandidateEmbeddings[candidateId].ModelName);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_MixedBatch_PerformsOneLookupAndSendsOnlyMisses()
    {
        var candidates = Enumerable.Range(0, 10)
            .Select(index => new SemanticCandidateDocument(Guid.NewGuid(), $"Candidate {index}"))
            .ToArray();
        var provider = new FakeEmbeddingProvider();
        var store = new FakeEmbeddingStore();
        store.JobEmbeddings[JobId] = Stored(JobId, "Job", new[] { 1f, 0f });
        for (var index = 0; index < 7; index++)
            store.CandidateEmbeddings[candidates[index].CandidateId] = Stored(candidates[index].CandidateId, candidates[index].Text, new[] { 1f, 0f });
        for (var index = 7; index < 10; index++)
            provider.DocumentMappings[candidates[index].Text] = new[] { 0f, 1f };
        var sut = CreateService(provider, store);

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 10);

        Assert.True(result.IsSuccess);
        Assert.Single(store.CandidateLookupCalls);
        Assert.Equal(10, store.CandidateLookupCalls[0].Count);
        Assert.Single(provider.DocumentCalls);
        Assert.Equal(candidates.Skip(7).Select(candidate => candidate.Text), provider.DocumentCalls[0]);
        Assert.Equal(3, Assert.Single(store.CandidateUpsertCalls).Count);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_HitsAndMisses_PreserveCandidateVectorAssociations()
    {
        var candidates = new[]
        {
            new SemanticCandidateDocument(Guid.Parse("10000000-0000-0000-0000-000000000000"), "A"),
            new SemanticCandidateDocument(Guid.Parse("20000000-0000-0000-0000-000000000000"), "B"),
            new SemanticCandidateDocument(Guid.Parse("30000000-0000-0000-0000-000000000000"), "C"),
            new SemanticCandidateDocument(Guid.Parse("40000000-0000-0000-0000-000000000000"), "D")
        };
        var provider = new FakeEmbeddingProvider();
        provider.DocumentMappings["A"] = new[] { 1f, 0f };
        provider.DocumentMappings["C"] = new[] { -1f, 0f };
        var store = new FakeEmbeddingStore();
        store.JobEmbeddings[JobId] = Stored(JobId, "Job", new[] { 1f, 0f });
        store.CandidateEmbeddings[candidates[3].CandidateId] = Stored(candidates[3].CandidateId, "D", new[] { 1f, 1f });
        store.CandidateEmbeddings[candidates[1].CandidateId] = Stored(candidates[1].CandidateId, "B", new[] { 0f, 1f });
        var sut = CreateService(provider, store);

        var result = await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 4);

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { "A", "C" }, Assert.Single(provider.DocumentCalls));
        var scores = result.Value.ToDictionary(item => item.CandidateId, item => item.CosineSimilarityScore);
        Assert.Equal(1d, scores[candidates[0].CandidateId], 10);
        Assert.Equal(0d, scores[candidates[1].CandidateId], 10);
        Assert.Equal(-1d, scores[candidates[2].CandidateId], 10);
        Assert.Equal(Math.Sqrt(0.5d), scores[candidates[3].CandidateId], 10);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_PersistentHits_KeepExistingRankingUnchanged()
    {
        var candidates = new[]
        {
            new SemanticCandidateDocument(Guid.NewGuid(), "A"),
            new SemanticCandidateDocument(Guid.NewGuid(), "B"),
            new SemanticCandidateDocument(Guid.NewGuid(), "C")
        };
        var vectors = new Dictionary<string, float[]>
        {
            ["A"] = new[] { 0.8f, 0.2f },
            ["B"] = new[] { 1f, 0f },
            ["C"] = new[] { 0f, 1f }
        };
        var missProvider = new FakeEmbeddingProvider();
        missProvider.QueryMappings["Job"] = new[] { 1f, 0f };
        foreach (var pair in vectors)
            missProvider.DocumentMappings[pair.Key] = pair.Value;
        var missResult = await CreateService(missProvider)
            .RetrieveTopCandidatesAsync(JobId, "Job", candidates, 3);

        var hitProvider = new FakeEmbeddingProvider { FailQueryCall = true, FailDocumentCall = true };
        var hitStore = new FakeEmbeddingStore();
        hitStore.JobEmbeddings[JobId] = Stored(JobId, "Job", new[] { 1f, 0f });
        foreach (var candidate in candidates)
            hitStore.CandidateEmbeddings[candidate.CandidateId] = Stored(candidate.CandidateId, candidate.Text, vectors[candidate.Text]);
        var hitResult = await CreateService(hitProvider, hitStore)
            .RetrieveTopCandidatesAsync(JobId, "Job", candidates, 3);

        Assert.True(missResult.IsSuccess);
        Assert.True(hitResult.IsSuccess);
        Assert.Equal(
            missResult.Value.Select(item => (item.CandidateId, item.CosineSimilarityScore)),
            hitResult.Value.Select(item => (item.CandidateId, item.CosineSimilarityScore)));
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_PropagatesCancellationToken()
    {
        bool queryTokenPropagated = false;
        bool documentTokenPropagated = false;
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        var mockProvider = new MockProvider(
            queryFn: t =>
            {
                queryTokenPropagated = (t == token);
                return Task.FromResult(Result<float[]>.Success(new float[] { 1f }));
            },
            documentFn: t =>
            {
                documentTokenPropagated = (t == token);
                return Task.FromResult(Result<IReadOnlyList<float[]>>.Success(new List<float[]> { new float[] { 1f } }));
            });

        var sut = CreateService(mockProvider, expectedDimension: 1);
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") };

        await sut.RetrieveTopCandidatesAsync(JobId, "Job", candidates, 10, token);

        Assert.True(queryTokenPropagated);
        Assert.True(documentTokenPropagated);
    }

    private class MockProvider : IEmbeddingProvider
    {
        private readonly Func<CancellationToken, Task<Result<float[]>>> _queryFn;
        private readonly Func<CancellationToken, Task<Result<IReadOnlyList<float[]>>>> _documentFn;

        public MockProvider(
            Func<CancellationToken, Task<Result<float[]>>> queryFn,
            Func<CancellationToken, Task<Result<IReadOnlyList<float[]>>>> documentFn)
        {
            _queryFn = queryFn;
            _documentFn = documentFn;
        }

        public Task<Result<float[]>> GenerateQueryEmbeddingAsync(string queryText, CancellationToken cancellationToken = default)
            => _queryFn(cancellationToken);

        public Task<Result<IReadOnlyList<float[]>>> GenerateDocumentEmbeddingsAsync(IReadOnlyList<string> documentTexts, CancellationToken cancellationToken = default)
            => _documentFn(cancellationToken);
    }
}
