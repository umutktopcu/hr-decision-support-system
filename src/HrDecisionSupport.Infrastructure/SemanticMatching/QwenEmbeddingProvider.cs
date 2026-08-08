using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.SemanticMatching.Embeddings;
using Microsoft.Extensions.Options;

namespace HrDecisionSupport.Infrastructure.SemanticMatching;

/// <summary>
/// Calls the local Qwen Python embedding service via HTTP.
/// Model-specific query instruction is applied server-side by the Python service.
/// </summary>
public sealed class QwenEmbeddingProvider : IEmbeddingProvider
{
    private readonly HttpClient _httpClient;
    private readonly QwenEmbeddingOptions _options;

    public QwenEmbeddingProvider(HttpClient httpClient, IOptions<QwenEmbeddingOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<Result<float[]>> GenerateQueryEmbeddingAsync(
        string queryText,
        CancellationToken cancellationToken = default)
    {
        var request = new EmbeddingRequest("query", new[] { queryText });
        var response = await PostEmbeddingsAsync(request, expectedCount: 1, cancellationToken);
        if (response.IsFailure)
            return Result<float[]>.Failure(response.Error!);

        return Result<float[]>.Success(response.Value[0]);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<float[]>>> GenerateDocumentEmbeddingsAsync(
        IReadOnlyList<string> documentTexts,
        CancellationToken cancellationToken = default)
    {
        var request = new EmbeddingRequest("document", documentTexts);
        var response = await PostEmbeddingsAsync(request, expectedCount: documentTexts.Count, cancellationToken);
        if (response.IsFailure)
            return Result<IReadOnlyList<float[]>>.Failure(response.Error!);

        return Result<IReadOnlyList<float[]>>.Success(response.Value);
    }

    private async Task<Result<IReadOnlyList<float[]>>> PostEmbeddingsAsync(
        EmbeddingRequest request,
        int expectedCount,
        CancellationToken cancellationToken)
    {
        HttpResponseMessage httpResponse;
        try
        {
            httpResponse = await _httpClient.PostAsJsonAsync("/embeddings", request, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw; // Do not swallow cancellation
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<float[]>>.Failure("EmbeddingService.Unavailable",
                $"Embedding service is unavailable: {ex.Message}");
        }

        if (!httpResponse.IsSuccessStatusCode)
        {
            var body = await TryReadBodyAsync(httpResponse, cancellationToken);
            return Result<IReadOnlyList<float[]>>.Failure("EmbeddingService.Error",
                $"Embedding service returned HTTP {(int)httpResponse.StatusCode}: {body}");
        }

        EmbeddingResponse? response;
        try
        {
            response = await httpResponse.Content.ReadFromJsonAsync<EmbeddingResponse>(cancellationToken: cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<IReadOnlyList<float[]>>.Failure("EmbeddingService.InvalidJson",
                $"Failed to deserialize embedding response: {ex.Message}");
        }

        if (response is null)
            return Result<IReadOnlyList<float[]>>.Failure("EmbeddingService.InvalidJson", "Embedding service returned null response.");

        if (response.Embeddings is null || response.Embeddings.Count != expectedCount)
            return Result<IReadOnlyList<float[]>>.Failure("EmbeddingService.WrongCount",
                $"Embedding service returned {response.Embeddings?.Count ?? 0} vectors, expected {expectedCount}.");

        var vectors = new float[expectedCount][];
        for (int i = 0; i < expectedCount; i++)
        {
            var vec = response.Embeddings[i];
            if (vec is null || vec.Count != _options.ExpectedDimension)
                return Result<IReadOnlyList<float[]>>.Failure("EmbeddingService.WrongDimension",
                    $"Vector {i} has dimension {vec?.Count ?? 0}, expected {_options.ExpectedDimension}.");

            var arr = new float[vec.Count];
            for (int j = 0; j < vec.Count; j++)
            {
                float val = vec[j];
                if (float.IsNaN(val) || float.IsInfinity(val))
                    return Result<IReadOnlyList<float[]>>.Failure("EmbeddingService.InvalidValue",
                        $"Vector {i} contains invalid value at index {j}: {val}");
                arr[j] = val;
            }
            vectors[i] = arr;
        }

        return Result<IReadOnlyList<float[]>>.Success(vectors);
    }

    private static async Task<string> TryReadBodyAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch
        {
            return "(body unavailable)";
        }
    }

    // Internal HTTP request/response shapes (mirror Python service models)
    private sealed record EmbeddingRequest(
        [property: JsonPropertyName("input_type")] string InputType,
        [property: JsonPropertyName("texts")] IReadOnlyList<string> Texts);

    private sealed class EmbeddingResponse
    {
        [JsonPropertyName("embeddings")]
        public List<List<float>>? Embeddings { get; set; }

        [JsonPropertyName("dimension")]
        public int Dimension { get; set; }

        [JsonPropertyName("count")]
        public int Count { get; set; }
    }
}
