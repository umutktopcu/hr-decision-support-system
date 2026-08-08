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
        public Dictionary<string, float[]> Mappings { get; } = new();
        public bool FailNextCall { get; set; }
        public bool ReturnWrongCount { get; set; }
        
        public Task<Result<IReadOnlyList<float[]>>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        {
            if (FailNextCall)
                return Task.FromResult(Result<IReadOnlyList<float[]>>.Failure("Provider", "Failed"));

            if (ReturnWrongCount)
                return Task.FromResult(Result<IReadOnlyList<float[]>>.Success(new List<float[]> { new float[] { 1f } }));

            var results = new List<float[]>();
            foreach (var text in texts)
            {
                if (Mappings.TryGetValue(text, out var vec))
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
        provider.Mappings["Job"] = new float[] { 1f, 0f };
        provider.Mappings["CandidateA"] = new float[] { 0.8f, 0.2f }; // dot=0.8, magB=~0.824, cos > 0
        provider.Mappings["CandidateB"] = new float[] { 1f, 0f };     // Identical to Job -> cos=1
        provider.Mappings["CandidateC"] = new float[] { 0f, 1f };     // Orthogonal to Job -> cos=0

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
    public async Task RetrieveTopCandidatesAsync_AppliesTopN_Correctly()
    {
        var provider = new FakeEmbeddingProvider();
        provider.Mappings["Job"] = new float[] { 1f, 0f };
        provider.Mappings["C1"] = new float[] { 1f, 0f };
        provider.Mappings["C2"] = new float[] { 1f, 0f };
        provider.Mappings["C3"] = new float[] { 1f, 0f };

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
        provider.Mappings["Job"] = new float[] { 1f, 0f };
        provider.Mappings["C1"] = new float[] { 1f, 0f };

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
        provider.Mappings["Job"] = new float[] { 1f, 0f };
        provider.Mappings["C1"] = new float[] { 1f, 0f };
        provider.Mappings["C2"] = new float[] { 1f, 0f };

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
        var provider = new FakeEmbeddingProvider { FailNextCall = true }; // Should not be called
        var sut = new SemanticRetrievalService(provider);

        var result = await sut.RetrieveTopCandidatesAsync("Job", new List<SemanticCandidateDocument>(), 10);
        
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
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
        var provider = new FakeEmbeddingProvider { ReturnWrongCount = true };
        var sut = new SemanticRetrievalService(provider);
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") };

        var result = await sut.RetrieveTopCandidatesAsync("Job", candidates, 10);
        
        Assert.True(result.IsFailure);
        Assert.Contains("expected 2", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_ProviderFails_PropagatesError()
    {
        var provider = new FakeEmbeddingProvider { FailNextCall = true };
        var sut = new SemanticRetrievalService(provider);
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") };

        var result = await sut.RetrieveTopCandidatesAsync("Job", candidates, 10);
        
        Assert.True(result.IsFailure);
        Assert.Equal("Failed", result.Error!.Message);
    }

    [Fact]
    public async Task RetrieveTopCandidatesAsync_DimensionMismatch_ReturnsFailure()
    {
        var provider = new FakeEmbeddingProvider();
        provider.Mappings["Job"] = new float[] { 1f, 0f };
        provider.Mappings["C1"] = new float[] { 1f, 0f, 0f }; // Mismatched dimension

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
        bool tokenPropagated = false;
        var cts = new CancellationTokenSource();
        var token = cts.Token;

        var mockProvider = new MockProvider(t => {
            tokenPropagated = (t == token);
            return Task.FromResult(Result<IReadOnlyList<float[]>>.Success(new List<float[]> { new float[] {1f}, new float[] {1f} }));
        });

        var sut = new SemanticRetrievalService(mockProvider);
        var candidates = new List<SemanticCandidateDocument> { new(Guid.NewGuid(), "C1") };

        await sut.RetrieveTopCandidatesAsync("Job", candidates, 10, token);
        
        Assert.True(tokenPropagated);
    }

    private class MockProvider : IEmbeddingProvider
    {
        private readonly Func<CancellationToken, Task<Result<IReadOnlyList<float[]>>>> _func;
        public MockProvider(Func<CancellationToken, Task<Result<IReadOnlyList<float[]>>>> func) => _func = func;
        public Task<Result<IReadOnlyList<float[]>>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default) => _func(cancellationToken);
    }
}
