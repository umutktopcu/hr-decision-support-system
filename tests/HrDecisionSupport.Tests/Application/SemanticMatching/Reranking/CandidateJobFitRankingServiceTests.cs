using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.SemanticMatching.Orchestration;
using HrDecisionSupport.Application.SemanticMatching.Orchestration.Models;
using HrDecisionSupport.Application.SemanticMatching.Reranking;
using HrDecisionSupport.Application.SemanticMatching.Reranking.Models;
using Moq;
using Xunit;

namespace HrDecisionSupport.Tests.Application.SemanticMatching.Reranking;

public class CandidateJobFitRankingServiceTests
{
    private readonly Mock<ICandidateSemanticMatchingService> _semanticMatchingMock;
    private readonly Mock<ICrossEncoderProvider> _crossEncoderMock;

    public CandidateJobFitRankingServiceTests()
    {
        _semanticMatchingMock = new Mock<ICandidateSemanticMatchingService>();
        _crossEncoderMock = new Mock<ICrossEncoderProvider>();
    }

    private CandidateJobFitRankingService CreateService()
    {
        return new CandidateJobFitRankingService(
            _semanticMatchingMock.Object,
            _crossEncoderMock.Object);
    }

    [Fact]
    public async Task RankApplicantsForJobAsync_B3Fails_PropagatesError()
    {
        var sut = CreateService();
        var jobId = Guid.NewGuid();

        _semanticMatchingMock.Setup(x => x.MatchApplicantsForJobAsync(jobId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateSemanticMatchingBatchResult>.Failure(new Error("b3_error", "Failed")));

        var result = await sut.RankApplicantsForJobAsync(jobId, 10, 5);

        Assert.True(result.IsFailure);
        Assert.Equal("b3_error", result.Error!.Code);
    }

    [Fact]
    public async Task RankApplicantsForJobAsync_EmptyRetrieved_BypassesCrossEncoder()
    {
        var sut = CreateService();
        var jobId = Guid.NewGuid();

        var b3Result = new CandidateSemanticMatchingBatchResult(
            JobRequisitionId: jobId,
            TotalApplicants: 5,
            PreScreeningEligibleCount: 5,
            PreScreeningRejectedCount: 0,
            RequestedTopN: 10,
            RetrievedCount: 0,
            JobDocumentText: "Job Doc",
            Results: Array.Empty<CandidateSemanticMatchingResult>()
        );

        _semanticMatchingMock.Setup(x => x.MatchApplicantsForJobAsync(jobId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateSemanticMatchingBatchResult>.Success(b3Result));

        var result = await sut.RankApplicantsForJobAsync(jobId, 10, 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.FinalCandidateCount);
        Assert.Empty(result.Value.Results);

        _crossEncoderMock.Verify(x => x.ScoreAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<CrossEncoderCandidateInput>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RankApplicantsForJobAsync_TierOrderingIsCorrect()
    {
        var sut = CreateService();
        var jobId = Guid.NewGuid();

        var c1 = Guid.NewGuid(); // 1/2 Mand, 1/1 Pref, CE=9.0
        var c2 = Guid.NewGuid(); // 2/2 Mand, 0/1 Pref, CE=5.0
        var c3 = Guid.NewGuid(); // 2/2 Mand, 1/1 Pref, CE=1.0

        var b3Result = new CandidateSemanticMatchingBatchResult(
            JobRequisitionId: jobId,
            TotalApplicants: 3,
            PreScreeningEligibleCount: 3,
            PreScreeningRejectedCount: 0,
            RequestedTopN: 10,
            RetrievedCount: 3,
            JobDocumentText: "Job Doc",
            Results: new[] { 
                new CandidateSemanticMatchingResult(c1, 0.9, "Doc", 1, 2, 0.5m, 1, 1, 1.0m),
                new CandidateSemanticMatchingResult(c2, 0.8, "Doc", 2, 2, 1.0m, 0, 1, 0.0m),
                new CandidateSemanticMatchingResult(c3, 0.7, "Doc", 2, 2, 1.0m, 1, 1, 1.0m),
            }
        );

        _semanticMatchingMock.Setup(x => x.MatchApplicantsForJobAsync(jobId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateSemanticMatchingBatchResult>.Success(b3Result));

        var crossEncoderResults = new List<CrossEncoderScoreResult>
        {
            new CrossEncoderScoreResult(c1, 9.0, 0.99),
            new CrossEncoderScoreResult(c2, 5.0, 0.90),
            new CrossEncoderScoreResult(c3, 1.0, 0.50)
        };

        _crossEncoderMock.Setup(x => x.ScoreAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<CrossEncoderCandidateInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CrossEncoderScoreResult>>.Success(crossEncoderResults));

        var result = await sut.RankApplicantsForJobAsync(jobId, 10, 10);

        Assert.True(result.IsSuccess);
        var batch = result.Value;
        
        // Ordering should be:
        // 1. c3 (Tier 1: 1.0 Mand, 1.0 Pref)
        // 2. c2 (Tier 2: 1.0 Mand, 0.0 Pref)
        // 3. c1 (Tier 3: 0.5 Mand, 1.0 Pref)
        Assert.Equal(3, batch.FinalCandidateCount);
        
        Assert.Equal(c3, batch.Results[0].CandidateId);
        Assert.Equal(1, batch.Results[0].SkillTier);
        Assert.Equal(1, batch.Results[0].FinalRank);

        Assert.Equal(c2, batch.Results[1].CandidateId);
        Assert.Equal(2, batch.Results[1].SkillTier);
        Assert.Equal(2, batch.Results[1].FinalRank);

        Assert.Equal(c1, batch.Results[2].CandidateId);
        Assert.Equal(3, batch.Results[2].SkillTier);
        Assert.Equal(3, batch.Results[2].FinalRank);

        // Verify CrossEncoder was called exactly once with ALL 3 candidates
        _crossEncoderMock.Verify(x => x.ScoreAsync(It.IsAny<string>(), It.Is<IReadOnlyList<CrossEncoderCandidateInput>>(l => l.Count == 3), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RankApplicantsForJobAsync_SameTier_SortsByRawScore()
    {
        var sut = CreateService();
        var jobId = Guid.NewGuid();

        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();

        // Both are identical on tier dimensions
        var b3Result = new CandidateSemanticMatchingBatchResult(
            JobRequisitionId: jobId,
            TotalApplicants: 2,
            PreScreeningEligibleCount: 2,
            PreScreeningRejectedCount: 0,
            RequestedTopN: 10,
            RetrievedCount: 2,
            JobDocumentText: "Job Doc",
            Results: new[] { 
                new CandidateSemanticMatchingResult(c1, 0.9, "Doc", 2, 2, 1.0m, 1, 1, 1.0m),
                new CandidateSemanticMatchingResult(c2, 0.8, "Doc", 2, 2, 1.0m, 1, 1, 1.0m),
            }
        );

        _semanticMatchingMock.Setup(x => x.MatchApplicantsForJobAsync(jobId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateSemanticMatchingBatchResult>.Success(b3Result));

        var crossEncoderResults = new List<CrossEncoderScoreResult>
        {
            new CrossEncoderScoreResult(c1, 2.0, 0.60), // Lower score
            new CrossEncoderScoreResult(c2, 8.0, 0.99), // Higher score
        };

        _crossEncoderMock.Setup(x => x.ScoreAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<CrossEncoderCandidateInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CrossEncoderScoreResult>>.Success(crossEncoderResults));

        var result = await sut.RankApplicantsForJobAsync(jobId, 10, 10);

        Assert.True(result.IsSuccess);
        var batch = result.Value;
        
        // c2 should win because of Raw CE Score
        Assert.Equal(c2, batch.Results[0].CandidateId);
        Assert.Equal(1, batch.Results[0].SkillTier);
        
        Assert.Equal(c1, batch.Results[1].CandidateId);
        Assert.Equal(1, batch.Results[1].SkillTier);
    }

    [Fact]
    public async Task RankApplicantsForJobAsync_AppliesFinalTopN_Correctly()
    {
        var sut = CreateService();
        var jobId = Guid.NewGuid();

        var c1 = Guid.NewGuid();
        var c2 = Guid.NewGuid();

        var b3Result = new CandidateSemanticMatchingBatchResult(
            JobRequisitionId: jobId,
            TotalApplicants: 2,
            PreScreeningEligibleCount: 2,
            PreScreeningRejectedCount: 0,
            RequestedTopN: 10,
            RetrievedCount: 2,
            JobDocumentText: "Job Doc",
            Results: new[] { 
                new CandidateSemanticMatchingResult(c1, 0.9, "Doc", 2, 2, 1.0m, 1, 1, 1.0m),
                new CandidateSemanticMatchingResult(c2, 0.8, "Doc", 2, 2, 1.0m, 1, 1, 1.0m),
            }
        );

        _semanticMatchingMock.Setup(x => x.MatchApplicantsForJobAsync(jobId, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateSemanticMatchingBatchResult>.Success(b3Result));

        var crossEncoderResults = new List<CrossEncoderScoreResult>
        {
            new CrossEncoderScoreResult(c1, 8.0, 0.99),
            new CrossEncoderScoreResult(c2, 2.0, 0.60)
        };

        _crossEncoderMock.Setup(x => x.ScoreAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<CrossEncoderCandidateInput>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<CrossEncoderScoreResult>>.Success(crossEncoderResults));

        var result = await sut.RankApplicantsForJobAsync(jobId, 10, 1); // finalTopN = 1

        Assert.True(result.IsSuccess);
        var batch = result.Value;
        
        Assert.Equal(1, batch.FinalCandidateCount);
        Assert.Single(batch.Results);
        Assert.Equal(c1, batch.Results[0].CandidateId);
    }
}
