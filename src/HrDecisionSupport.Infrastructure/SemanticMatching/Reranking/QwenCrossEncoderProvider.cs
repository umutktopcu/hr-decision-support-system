using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.SemanticMatching.Reranking;
using HrDecisionSupport.Application.SemanticMatching.Reranking.Models;

namespace HrDecisionSupport.Infrastructure.SemanticMatching.Reranking;

public class QwenCrossEncoderProvider : ICrossEncoderProvider
{
    private readonly HttpClient _httpClient;

    public QwenCrossEncoderProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Result<IReadOnlyList<CrossEncoderScoreResult>>> ScoreAsync(
        string jobDocument, 
        IReadOnlyList<CrossEncoderCandidateInput> candidates, 
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(jobDocument))
            return Result<IReadOnlyList<CrossEncoderScoreResult>>.Failure(new Error("invalid_input", "Job document is empty.", ErrorType.Validation));
        
        if (candidates == null || !candidates.Any())
            return Result<IReadOnlyList<CrossEncoderScoreResult>>.Success(Array.Empty<CrossEncoderScoreResult>());

        var request = new RerankRequestDto
        {
            JobDocument = jobDocument,
            Candidates = candidates.Select(c => new CandidateInputDto
            {
                CandidateId = c.CandidateId.ToString(),
                CandidateDocument = c.CandidateDocumentText
            }).ToList()
        };

        try
        {
            var response = await _httpClient.PostAsJsonAsync("/rerank", request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Result<IReadOnlyList<CrossEncoderScoreResult>>.Failure(
                    new Error("reranker_http_error", $"Reranker service returned {(int)response.StatusCode}.", ErrorType.Failure));
            }

            var resultDto = await response.Content.ReadFromJsonAsync<RerankResponseDto>(cancellationToken: cancellationToken);
            if (resultDto == null || resultDto.Results == null)
            {
                return Result<IReadOnlyList<CrossEncoderScoreResult>>.Failure(
                    new Error("reranker_deserialization_error", "Failed to deserialize reranker response.", ErrorType.Failure));
            }

            if (resultDto.Results.Count != candidates.Count)
            {
                return Result<IReadOnlyList<CrossEncoderScoreResult>>.Failure(
                    new Error("reranker_count_mismatch", $"Expected {candidates.Count} results, got {resultDto.Results.Count}.", ErrorType.Failure));
            }

            var requestedIds = candidates.Select(c => c.CandidateId).ToHashSet();
            var returnedIds = new HashSet<Guid>();

            var finalResults = new List<CrossEncoderScoreResult>(resultDto.Results.Count);
            foreach (var r in resultDto.Results)
            {
                if (!Guid.TryParse(r.CandidateId, out var candidateId))
                {
                    return Result<IReadOnlyList<CrossEncoderScoreResult>>.Failure(
                        new Error("reranker_invalid_candidate_id", $"Invalid CandidateId in response: {r.CandidateId}", ErrorType.Failure));
                }

                if (double.IsNaN(r.JobFitScore) || double.IsInfinity(r.JobFitScore))
                {
                    return Result<IReadOnlyList<CrossEncoderScoreResult>>.Failure(
                        new Error("reranker_invalid_score", "JobFitScore is NaN or Infinity.", ErrorType.Failure));
                }

                if (!requestedIds.Contains(candidateId))
                {
                    return Result<IReadOnlyList<CrossEncoderScoreResult>>.Failure(
                        new Error("reranker_unknown_candidate_id", $"CandidateId in response was not requested: {candidateId}", ErrorType.Failure));
                }

                if (!returnedIds.Add(candidateId))
                {
                    return Result<IReadOnlyList<CrossEncoderScoreResult>>.Failure(
                        new Error("reranker_duplicate_candidate_id", $"Duplicate CandidateId in response: {candidateId}", ErrorType.Failure));
                }

                var boundedScore = Math.Max(0.0, Math.Min(1.0, r.JobFitScore));

                finalResults.Add(new CrossEncoderScoreResult(
                    CandidateId: candidateId,
                    RawScore: r.RawScore,
                    JobFitScore: boundedScore
                ));
            }

            return Result<IReadOnlyList<CrossEncoderScoreResult>>.Success(finalResults);
        }
        catch (HttpRequestException ex)
        {
            return Result<IReadOnlyList<CrossEncoderScoreResult>>.Failure(
                new Error("reranker_connection_error", $"Failed to connect to reranker service: {ex.Message}", ErrorType.Failure));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<IReadOnlyList<CrossEncoderScoreResult>>.Failure(
                new Error("reranker_unexpected_error", $"Unexpected error during reranking: {ex.Message}", ErrorType.Failure));
        }
    }

    private class RerankRequestDto
    {
        [JsonPropertyName("job_document")]
        public string JobDocument { get; set; } = string.Empty;

        [JsonPropertyName("candidates")]
        public List<CandidateInputDto> Candidates { get; set; } = new();
    }

    private class CandidateInputDto
    {
        [JsonPropertyName("candidate_id")]
        public string CandidateId { get; set; } = string.Empty;

        [JsonPropertyName("candidate_document")]
        public string CandidateDocument { get; set; } = string.Empty;
    }

    private class RerankResponseDto
    {
        [JsonPropertyName("results")]
        public List<RerankResultDto> Results { get; set; } = new();
    }

    private class RerankResultDto
    {
        [JsonPropertyName("candidate_id")]
        public string CandidateId { get; set; } = string.Empty;

        [JsonPropertyName("raw_score")]
        public double RawScore { get; set; }

        [JsonPropertyName("job_fit_score")]
        public double JobFitScore { get; set; }
    }
}
