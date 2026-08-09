using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.SemanticMatching.Reranking.Models;
using HrDecisionSupport.Infrastructure.SemanticMatching.Reranking;
using Moq;
using Moq.Protected;
using Xunit;

namespace HrDecisionSupport.Tests.SemanticMatching.Reranking.ProviderTests;

public class QwenCrossEncoderProviderTests
{
    private (HttpClient, Mock<HttpMessageHandler>) CreateMockHttpClient(HttpStatusCode statusCode, string responseContent)
    {
        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(responseContent)
            });

        var client = new HttpClient(handlerMock.Object)
        {
            BaseAddress = new Uri("http://localhost:8766/")
        };

        return (client, handlerMock);
    }

    [Fact]
    public async Task ScoreAsync_EmptyJobDocument_ReturnsValidationError()
    {
        var (client, _) = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var sut = new QwenCrossEncoderProvider(client);

        var result = await sut.ScoreAsync("", new[] { new CrossEncoderCandidateInput(Guid.NewGuid(), "doc") });

        Assert.True(result.IsFailure);
        Assert.Equal("invalid_input", result.Error!.Code);
    }

    [Fact]
    public async Task ScoreAsync_EmptyCandidates_ReturnsEmptySuccessWithoutCallingHttp()
    {
        var (client, handlerMock) = CreateMockHttpClient(HttpStatusCode.OK, "{}");
        var sut = new QwenCrossEncoderProvider(client);

        var result = await sut.ScoreAsync("Job Doc", Array.Empty<CrossEncoderCandidateInput>());

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        
        handlerMock.Protected().Verify(
            "SendAsync",
            Times.Never(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task ScoreAsync_HttpError_ReturnsFailure()
    {
        var (client, _) = CreateMockHttpClient(HttpStatusCode.InternalServerError, "{}");
        var sut = new QwenCrossEncoderProvider(client);
        
        var result = await sut.ScoreAsync("Job Doc", new[] { new CrossEncoderCandidateInput(Guid.NewGuid(), "doc") });

        Assert.True(result.IsFailure);
        Assert.Equal("reranker_http_error", result.Error!.Code);
    }

    [Fact]
    public async Task ScoreAsync_ValidResponse_MapsCorrectly()
    {
        var candidateId = Guid.NewGuid();
        var jsonResponse = $@"
        {{
            ""results"": [
                {{
                    ""candidate_id"": ""{candidateId}"",
                    ""raw_score"": 4.5,
                    ""job_fit_score"": 0.95
                }}
            ]
        }}";

        var (client, _) = CreateMockHttpClient(HttpStatusCode.OK, jsonResponse);
        var sut = new QwenCrossEncoderProvider(client);

        var result = await sut.ScoreAsync("Job Doc", new[] { new CrossEncoderCandidateInput(candidateId, "doc") });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal(candidateId, result.Value[0].CandidateId);
        Assert.Equal(4.5, result.Value[0].RawScore);
        Assert.Equal(0.95, result.Value[0].JobFitScore);
    }

    [Fact]
    public async Task ScoreAsync_InvalidCandidateIdInResponse_ReturnsFailure()
    {
        var jsonResponse = $@"
        {{
            ""results"": [
                {{
                    ""candidate_id"": ""not-a-guid"",
                    ""raw_score"": 4.5,
                    ""job_fit_score"": 0.95
                }}
            ]
        }}";

        var (client, _) = CreateMockHttpClient(HttpStatusCode.OK, jsonResponse);
        var sut = new QwenCrossEncoderProvider(client);

        var result = await sut.ScoreAsync("Job Doc", new[] { new CrossEncoderCandidateInput(Guid.NewGuid(), "doc") });

        Assert.True(result.IsFailure);
        Assert.Equal("reranker_invalid_candidate_id", result.Error!.Code);
    }

    [Fact]
    public async Task ScoreAsync_BoundsScore_BetweenZeroAndOne()
    {
        var candidateId = Guid.NewGuid();
        var jsonResponse = $@"
        {{
            ""results"": [
                {{
                    ""candidate_id"": ""{candidateId}"",
                    ""raw_score"": 10.0,
                    ""job_fit_score"": 1.5
                }}
            ]
        }}";

        var (client, _) = CreateMockHttpClient(HttpStatusCode.OK, jsonResponse);
        var sut = new QwenCrossEncoderProvider(client);

        var result = await sut.ScoreAsync("Job Doc", new[] { new CrossEncoderCandidateInput(candidateId, "doc") });

        Assert.True(result.IsSuccess);
        // Because 1.5 is > 1.0, the provider should cap it at 1.0
        Assert.Equal(1.0, result.Value[0].JobFitScore);
    }

    [Fact]
    public async Task ScoreAsync_WrongResultCount_ReturnsFailure()
    {
        var candidateId1 = Guid.NewGuid();
        var candidateId2 = Guid.NewGuid();
        
        var jsonResponse = $@"
        {{
            ""results"": [
                {{
                    ""candidate_id"": ""{candidateId1}"",
                    ""raw_score"": 4.5,
                    ""job_fit_score"": 0.95
                }}
            ]
        }}"; // Only 1 returned, but 2 requested

        var (client, _) = CreateMockHttpClient(HttpStatusCode.OK, jsonResponse);
        var sut = new QwenCrossEncoderProvider(client);

        var result = await sut.ScoreAsync("Job Doc", new[] { 
            new CrossEncoderCandidateInput(candidateId1, "doc1"),
            new CrossEncoderCandidateInput(candidateId2, "doc2")
        });

        Assert.True(result.IsFailure);
        Assert.Equal("reranker_count_mismatch", result.Error!.Code);
    }

    [Fact]
    public async Task ScoreAsync_DuplicateCandidateId_ReturnsFailure()
    {
        var candidateId = Guid.NewGuid();
        
        var jsonResponse = $@"
        {{
            ""results"": [
                {{
                    ""candidate_id"": ""{candidateId}"",
                    ""raw_score"": 4.5,
                    ""job_fit_score"": 0.95
                }},
                {{
                    ""candidate_id"": ""{candidateId}"",
                    ""raw_score"": 3.0,
                    ""job_fit_score"": 0.80
                }}
            ]
        }}";

        var (client, _) = CreateMockHttpClient(HttpStatusCode.OK, jsonResponse);
        var sut = new QwenCrossEncoderProvider(client);

        // Requested 2 different candidates, but received same candidate ID twice
        var result = await sut.ScoreAsync("Job Doc", new[] { 
            new CrossEncoderCandidateInput(candidateId, "doc1"),
            new CrossEncoderCandidateInput(Guid.NewGuid(), "doc2")
        });

        Assert.True(result.IsFailure);
        Assert.Equal("reranker_duplicate_candidate_id", result.Error!.Code);
    }

    [Fact]
    public async Task ScoreAsync_UnknownCandidateId_ReturnsFailure()
    {
        var unknownId = Guid.NewGuid();
        
        var jsonResponse = $@"
        {{
            ""results"": [
                {{
                    ""candidate_id"": ""{unknownId}"",
                    ""raw_score"": 4.5,
                    ""job_fit_score"": 0.95
                }}
            ]
        }}";

        var (client, _) = CreateMockHttpClient(HttpStatusCode.OK, jsonResponse);
        var sut = new QwenCrossEncoderProvider(client);

        var result = await sut.ScoreAsync("Job Doc", new[] { 
            new CrossEncoderCandidateInput(Guid.NewGuid(), "doc1")
        });

        Assert.True(result.IsFailure);
        Assert.Equal("reranker_unknown_candidate_id", result.Error!.Code);
    }
}
