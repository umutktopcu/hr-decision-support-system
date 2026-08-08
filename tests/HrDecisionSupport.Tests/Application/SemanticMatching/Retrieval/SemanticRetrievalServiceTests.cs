using System;
using System.Collections.Generic;
using System.Linq;
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
    private class FakeEmbeddingProvider : IEmbeddingProvider
    {
        public Dictionary<string, float[]> QueryMappings { get; } = new();
        public Dictionary<string, float[]> DocumentMappings { get; } = new();
        public bool FailQueryCall { get; set; }
        public bool FailDocumentCall { get; set; }
        public bool ReturnWrongDocumentCount { get; set; }
        public List<string> QueryCallTexts { get; } = new();
        public List<string> DocumentCallTexts { get; } = new();

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

    [Fact]
    public async Task RetrieveTopCandidatesAsync_SortsDescendingByCosineScore()
    {
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["Job"] = new float[] { 1f, 0f };
        provider.DocumentMappings["CandidateA"] = new float[] { 0.8f, 0.2f }; // dot=0.8, magB=~0.824, cos > 0
        provider.DocumentMappings["CandidateB"] = new float[] { 1f, 0f };     // Identical to Job -> cos=1
        provider.DocumentMappings["CandidateC"] = new float[] { 0f, 1f };     // Orthogonal to Job -> cos=0

        var sut = new SemanticRetrievalService(provider);
        var candidates = new List<SemanticCandidateDocument>
        {
            new(Guid.NewGuid(), "CandidateA"),
            new(Guid.NewGuid(), "CandidateB"),
            new(Guid.NewGuid(), "CandidateC")
        };

        var result = await sut.RetrieveTopCandidatesAsync("Job", candidates, 3);

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

        var sut = new SemanticRetrievalService(provider);
        var candidates = new List<SemanticCandidateDocument>
        {
            new(Guid.NewGuid(), "CandidateDoc")
        };

        await sut.RetrieveTopCandidatesAsync("JobText", candidates, 10);

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

        var sut = new SemanticRetrievalService(provider);
        var candidates = new List<SemanticCandidateDocument>
        {
            new(Guid.NewGuid(), "C1"),
            new(Guid.NewGuid(), "C2")
        };

        await sut.RetrieveTopCandidatesAsync("Job", candidates, 10);

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

        var sut = new SemanticRetrievalService(provider);
        var candidates = new List<SemanticCandidateDocument>
        {
            new(Guid.NewGuid(), "C1"),
            new(Guid.NewGuid(), "C2"),
            new(Guid.NewGuid(), "C3")
        };

        var result = await sut.RetrieveTopCandidatesAsync("Job", candidates, 2);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_TopNGreaterThanPool_ReturnsAll()
    {
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["Job"] = new float[] { 1f, 0f };
        provider.DocumentMappings["C1"] = new float[] { 1f, 0f };

        var sut = new SemanticRetrievalService(provider);
        var candidates = new List<SemanticCandidateDocument>
        {
            new(Guid.NewGuid(), "C1")
        };

        var result = await sut.RetrieveTopCandidatesAsync("Job", candidates, 50);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_InvalidTopN_ReturnsFailure()
    {
        var sut = new SemanticRetrievalService(new FakeEmbeddingProvider());

        var result = await sut.RetrieveTopCandidatesAsync("Job", new List<SemanticCandidateDocument>(), 0);
        Assert.True(result.IsFailure);

        var result2 = await sut.RetrieveTopCandidatesAsync("Job", new List<SemanticCandidateDocument>(), -1);
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

        var sut = new SemanticRetrievalService(provider);

        // Pass them in reverse order to ensure sorting logic works
        var candidates = new List<SemanticCandidateDocument>
        {
            new(id2, "C2"),
            new(id1, "C1")
        };

        var result = await sut.RetrieveTopCandidatesAsync("Job", candidates, 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(id1, result.Value[0].CandidateId); // Secondary tie break ASC by Guid
        Assert.Equal(id2, result.Value[1].CandidateId);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_EmptyPool_ReturnsEmptyWithoutCallingProvider()
    {
        var provider = new FakeEmbeddingProvider { FailQueryCall = true, FailDocumentCall = true }; // Should not be called
        var sut = new SemanticRetrievalService(provider);

        var result = await sut.RetrieveTopCandidatesAsync("Job", new List<SemanticCandidateDocument>(), 10);

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

        var sut = new SemanticRetrievalService(new FakeEmbeddingProvider());

        var result = await sut.RetrieveTopCandidatesAsync("Job", candidates, 10);

        Assert.True(result.IsFailure);
        Assert.Contains("Duplicate candidate ID", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_EmptyJobDocument_ReturnsFailure()
    {
        var sut = new SemanticRetrievalService(new FakeEmbeddingProvider());

        var result = await sut.RetrieveTopCandidatesAsync("   ", new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") }, 10);

        Assert.True(result.IsFailure);
        Assert.Contains("Job document cannot be null or whitespace", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_EmptyCandidateDocument_ReturnsFailure()
    {
        var sut = new SemanticRetrievalService(new FakeEmbeddingProvider());
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "") };

        var result = await sut.RetrieveTopCandidatesAsync("Job", candidates, 10);

        Assert.True(result.IsFailure);
        Assert.Contains("empty", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_ProviderReturnsWrongCount_Detected()
    {
        var provider = new FakeEmbeddingProvider { ReturnWrongDocumentCount = true };
        var sut = new SemanticRetrievalService(provider);
        // Send 2 candidates but FakeEmbeddingProvider returns only 1 vector → count mismatch
        var candidates = new List<SemanticCandidateDocument>
        {
            new(Guid.NewGuid(), "C1"),
            new(Guid.NewGuid(), "C2")
        };

        var result = await sut.RetrieveTopCandidatesAsync("Job", candidates, 10);

        Assert.True(result.IsFailure);
        Assert.Contains("expected 2", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_QueryProviderFails_PropagatesError()
    {
        var provider = new FakeEmbeddingProvider { FailQueryCall = true };
        var sut = new SemanticRetrievalService(provider);
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") };

        var result = await sut.RetrieveTopCandidatesAsync("Job", candidates, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("Query embedding failed", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_DocumentProviderFails_PropagatesError()
    {
        var provider = new FakeEmbeddingProvider { FailDocumentCall = true };
        var sut = new SemanticRetrievalService(provider);
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") };

        var result = await sut.RetrieveTopCandidatesAsync("Job", candidates, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("Document embedding failed", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_DimensionMismatch_ReturnsFailure()
    {
        var provider = new FakeEmbeddingProvider();
        provider.QueryMappings["Job"] = new float[] { 1f, 0f };
        provider.DocumentMappings["C1"] = new float[] { 1f, 0f, 0f }; // Mismatched dimension

        var sut = new SemanticRetrievalService(provider);
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") };

        var result = await sut.RetrieveTopCandidatesAsync("Job", candidates, 10);

        Assert.True(result.IsFailure);
        Assert.Contains("Failed to calculate cosine similarity", result.Error!.Message);
        Assert.Contains("Dimension mismatch", result.Error.Message);
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

        var sut = new SemanticRetrievalService(mockProvider);
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") };

        await sut.RetrieveTopCandidatesAsync("Job", candidates, 10, token);

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
