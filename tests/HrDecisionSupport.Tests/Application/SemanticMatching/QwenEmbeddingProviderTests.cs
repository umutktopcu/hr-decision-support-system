using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.SemanticMatching.Embeddings;
using HrDecisionSupport.Infrastructure.SemanticMatching;
using Microsoft.Extensions.Options;
using Xunit;

namespace HrDecisionSupport.Tests.Application.SemanticMatching;

/// <summary>
/// Tests for QwenEmbeddingProvider using a fake HTTP handler.
/// Does NOT call the real Qwen Python service or Hugging Face model.
/// </summary>
public class QwenEmbeddingProviderTests
{
    private static QwenEmbeddingOptions DefaultOptions => new()
    {
        BaseUrl = "http://localhost:8765",
        TimeoutSeconds = 30,
        ExpectedDimension = 4 // Use dim=4 for test simplicity
    };

    private static QwenEmbeddingProvider CreateProvider(
        FakeHttpMessageHandler handler,
        QwenEmbeddingOptions? options = null)
    {
        var opts = options ?? DefaultOptions;
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri(opts.BaseUrl) };
        return new QwenEmbeddingProvider(httpClient, new OptionsWrapper<QwenEmbeddingOptions>(opts));
    }

    private static string BuildEmbeddingJson(List<List<float>> embeddings)
    {
        return JsonSerializer.Serialize(new
        {
            embeddings,
            dimension = embeddings.FirstOrDefault()?.Count ?? 0,
            count = embeddings.Count
        });
    }

    // ------------------------------------------------------------------ //
    // Query tests
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task GenerateQueryEmbeddingAsync_SendsInputTypeQuery()
    {
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedBody = req.Content?.ReadAsStringAsync().Result;
            var json = BuildEmbeddingJson(new List<List<float>> { new() { 0.1f, 0.2f, 0.3f, 0.4f } });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var provider = CreateProvider(handler);
        await provider.GenerateQueryEmbeddingAsync("job text");

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        Assert.Equal("query", doc.RootElement.GetProperty("input_type").GetString());
        Assert.Equal("job text", doc.RootElement.GetProperty("texts")[0].GetString());
    }

    [Fact]
    public async Task GenerateQueryEmbeddingAsync_ReturnsCorrectVector()
    {
        var expected = new List<float> { 0.1f, 0.2f, 0.3f, 0.4f };
        var handler = FakeHttpMessageHandler.WithJson(BuildEmbeddingJson(new() { expected }));

        var provider = CreateProvider(handler);
        var result = await provider.GenerateQueryEmbeddingAsync("query");

        Assert.True(result.IsSuccess);
        Assert.Equal(expected.Count, result.Value.Length);
        for (int i = 0; i < expected.Count; i++)
            Assert.Equal(expected[i], result.Value[i], precision: 5);
    }

    // ------------------------------------------------------------------ //
    // Document tests
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task GenerateDocumentEmbeddingsAsync_SendsInputTypeDocument()
    {
        string? capturedBody = null;
        var handler = new FakeHttpMessageHandler(req =>
        {
            capturedBody = req.Content?.ReadAsStringAsync().Result;
            var json = BuildEmbeddingJson(new List<List<float>>
            {
                new() { 0.1f, 0.2f, 0.3f, 0.4f },
                new() { 0.5f, 0.6f, 0.7f, 0.8f }
            });
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        });

        var provider = CreateProvider(handler);
        await provider.GenerateDocumentEmbeddingsAsync(new[] { "doc1", "doc2" });

        Assert.NotNull(capturedBody);
        using var doc = JsonDocument.Parse(capturedBody);
        Assert.Equal("document", doc.RootElement.GetProperty("input_type").GetString());
        var texts = doc.RootElement.GetProperty("texts");
        Assert.Equal(2, texts.GetArrayLength());
        Assert.Equal("doc1", texts[0].GetString());
        Assert.Equal("doc2", texts[1].GetString());
    }

    [Fact]
    public async Task GenerateDocumentEmbeddingsAsync_ReturnsCorrectVectors()
    {
        var vecs = new List<List<float>>
        {
            new() { 1f, 0f, 0f, 0f },
            new() { 0f, 1f, 0f, 0f },
            new() { 0f, 0f, 1f, 0f }
        };
        var handler = FakeHttpMessageHandler.WithJson(BuildEmbeddingJson(vecs));

        var provider = CreateProvider(handler);
        var result = await provider.GenerateDocumentEmbeddingsAsync(new[] { "a", "b", "c" });

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.Equal(1f, result.Value[0][0]);
        Assert.Equal(1f, result.Value[1][1]);
        Assert.Equal(1f, result.Value[2][2]);
    }

    // ------------------------------------------------------------------ //
    // Error handling
    // ------------------------------------------------------------------ //

    [Fact]
    public async Task GenerateQueryEmbeddingAsync_ServiceUnavailable_ReturnsFailure()
    {
        var handler = new FakeHttpMessageHandler(_ => throw new HttpRequestException("Connection refused"));
        var provider = CreateProvider(handler);

        var result = await provider.GenerateQueryEmbeddingAsync("text");

        Assert.True(result.IsFailure);
        Assert.Contains("unavailable", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateDocumentEmbeddingsAsync_NonSuccessStatus_ReturnsFailure()
    {
        var handler = FakeHttpMessageHandler.WithStatus(HttpStatusCode.InternalServerError, "internal error");
        var provider = CreateProvider(handler);

        var result = await provider.GenerateDocumentEmbeddingsAsync(new[] { "doc" });

        Assert.True(result.IsFailure);
        Assert.Contains("500", result.Error!.Message);
    }

    [Fact]
    public async Task GenerateDocumentEmbeddingsAsync_InvalidJson_ReturnsFailure()
    {
        var handler = FakeHttpMessageHandler.WithJson("not valid json at all");
        var provider = CreateProvider(handler);

        var result = await provider.GenerateDocumentEmbeddingsAsync(new[] { "doc" });

        Assert.True(result.IsFailure);
        Assert.Contains("deserialize", result.Error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task GenerateDocumentEmbeddingsAsync_WrongVectorCount_ReturnsFailure()
    {
        // Service returns 2 vectors but we sent 3 texts
        var json = BuildEmbeddingJson(new List<List<float>>
        {
            new() { 1f, 0f, 0f, 0f },
            new() { 0f, 1f, 0f, 0f }
        });
        var handler = FakeHttpMessageHandler.WithJson(json);
        var provider = CreateProvider(handler);

        var result = await provider.GenerateDocumentEmbeddingsAsync(new[] { "a", "b", "c" });

        Assert.True(result.IsFailure);
        Assert.Equal("EmbeddingService.WrongCount", result.Error!.Code);
    }

    [Fact]
    public async Task GenerateDocumentEmbeddingsAsync_WrongDimension_ReturnsFailure()
    {
        // Options expect dim=4 but service returns dim=2
        var json = BuildEmbeddingJson(new List<List<float>> { new() { 1f, 0f } });
        var handler = FakeHttpMessageHandler.WithJson(json);
        var provider = CreateProvider(handler);

        var result = await provider.GenerateDocumentEmbeddingsAsync(new[] { "doc" });

        Assert.True(result.IsFailure);
        Assert.Equal("EmbeddingService.WrongDimension", result.Error!.Code);
    }

    [Fact]
    public async Task GenerateDocumentEmbeddingsAsync_NaNValue_ReturnsFailure()
    {
        // Inject NaN via raw JSON
        const string json = """{"embeddings":[[null,0.1,0.2,0.3]],"dimension":4,"count":1}""";
        var handler = FakeHttpMessageHandler.WithJson(json);
        var provider = CreateProvider(handler);

        // null → 0f in deserialization which is valid; use actual NaN
        // We test the NaN path via a custom JSON
        const string nanJson = """{"embeddings":[[1.0,"NaN",0.2,0.3]],"dimension":4,"count":1}""";
        var handler2 = FakeHttpMessageHandler.WithJson(nanJson);
        var provider2 = CreateProvider(handler2);

        // JSON.NET will fail to deserialize "NaN" as float → InvalidJson failure
        var result = await provider2.GenerateDocumentEmbeddingsAsync(new[] { "doc" });
        Assert.True(result.IsFailure);
    }

    [Fact]
    public async Task GenerateQueryEmbeddingAsync_CancellationToken_Propagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancelled

        var handler = FakeHttpMessageHandler.WithJson(BuildEmbeddingJson(new() { new() { 1f, 0f, 0f, 0f } }));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GenerateQueryEmbeddingAsync("text", cts.Token));
    }

    [Fact]
    public async Task GenerateDocumentEmbeddingsAsync_CancellationToken_Propagated()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var handler = FakeHttpMessageHandler.WithJson(BuildEmbeddingJson(new() { new() { 1f, 0f, 0f, 0f } }));
        var provider = CreateProvider(handler);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => provider.GenerateDocumentEmbeddingsAsync(new[] { "doc" }, cts.Token));
    }

    // ------------------------------------------------------------------ //
    // Helper
    // ------------------------------------------------------------------ //

    internal class FakeHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        public static FakeHttpMessageHandler WithJson(string json) =>
            new(_ => new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            });

        public static FakeHttpMessageHandler WithStatus(HttpStatusCode status, string body) =>
            new(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "text/plain")
            });

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_handler(request));
        }
    }
}
