using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.MatchingExecution;
using HrDecisionSupport.Application.MatchingExecution.History;
using HrDecisionSupport.Application.MatchingExecution.Models;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.PreScreening.Models;
using HrDecisionSupport.Application.SemanticMatching.Reranking;
using HrDecisionSupport.Application.SemanticMatching.Reranking.Models;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace HrDecisionSupport.Tests.Application.MatchingExecution;

public class JobMatchingExecutionServiceAdditionalTests
{
    private readonly HrDecisionSupportDbContext _dbContext;
    private readonly Mock<ICandidateJobFitRankingService> _semanticMatchingServiceMock;
    private readonly Mock<IValidator<JobMatchingRequest>> _validatorMock;
    private readonly Mock<IMlPredictionService> _retentionPredictionServiceMock;
    private readonly Mock<IJobMatchingHistoryService> _historyServiceMock;
    private readonly JobMatchingExecutionService _sut;

    public JobMatchingExecutionServiceAdditionalTests()
    {
        var options = new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new HrDecisionSupportDbContext(options);

        _semanticMatchingServiceMock = new Mock<ICandidateJobFitRankingService>();
        _validatorMock = new Mock<IValidator<JobMatchingRequest>>();
        _retentionPredictionServiceMock = new Mock<IMlPredictionService>();
        _historyServiceMock = new Mock<IJobMatchingHistoryService>();
        _validatorMock.Setup(x => x.Validate(It.IsAny<JobMatchingRequest>())).Returns(ValidationResult.Valid());
        _historyServiceMock
            .Setup(service => service.SaveCompletedRunAsync(
                It.IsAny<CompletedJobMatchingRun>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        _sut = new JobMatchingExecutionService(
            _dbContext,
            _semanticMatchingServiceMock.Object,
            _validatorMock.Object,
            _retentionPredictionServiceMock.Object,
            _historyServiceMock.Object);
    }

    [Fact]
    public async Task ExecuteMatchingAsync_Pool_DeterminesSamePositionCorrectly_AndExcludesDifferentPosition()
    {
        var positionId = Guid.NewGuid();
        var diffPositionId = Guid.NewGuid();
        var targetJobId = Guid.NewGuid();

        _dbContext.JobRequisitions.Add(new JobRequisition { Id = targetJobId, PositionId = positionId, Title = "Target", RequisitionCode = "REQ1" });

        var sameJob1 = Guid.NewGuid();
        var sameJob2 = Guid.NewGuid();
        var diffJob = Guid.NewGuid();

        _dbContext.JobRequisitions.AddRange(
            new JobRequisition { Id = sameJob1, PositionId = positionId, Title = "Same 1", RequisitionCode = "REQ2" },
            new JobRequisition { Id = sameJob2, PositionId = positionId, Title = "Same 2", RequisitionCode = "REQ3" },
            new JobRequisition { Id = diffJob, PositionId = diffPositionId, Title = "Diff", RequisitionCode = "REQ4" }
        );

        var candidate1 = Guid.NewGuid(); // same 1
        var candidate2 = Guid.NewGuid(); // same 2
        var candidate3 = Guid.NewGuid(); // diff
        var candidateShared = Guid.NewGuid(); // both same 1 and same 2

        _dbContext.Candidates.AddRange(
            new Candidate { Id = candidate1, CandidateCode = "C1" },
            new Candidate { Id = candidate2, CandidateCode = "C2" },
            new Candidate { Id = candidate3, CandidateCode = "C3" },
            new Candidate { Id = candidateShared, CandidateCode = "CS" }
        );

        _dbContext.CandidateEvaluationCases.AddRange(
            new CandidateEvaluationCase { Id = Guid.NewGuid(), JobRequisitionId = sameJob1, CandidateId = candidate1, Status = CandidateEvaluationStatus.New },
            new CandidateEvaluationCase { Id = Guid.NewGuid(), JobRequisitionId = sameJob2, CandidateId = candidate2, Status = CandidateEvaluationStatus.New },
            new CandidateEvaluationCase { Id = Guid.NewGuid(), JobRequisitionId = diffJob, CandidateId = candidate3, Status = CandidateEvaluationStatus.New },
            new CandidateEvaluationCase { Id = Guid.NewGuid(), JobRequisitionId = sameJob1, CandidateId = candidateShared, Status = CandidateEvaluationStatus.New },
            new CandidateEvaluationCase { Id = Guid.NewGuid(), JobRequisitionId = sameJob2, CandidateId = candidateShared, Status = CandidateEvaluationStatus.New }
        );
        await _dbContext.SaveChangesAsync();

        var request = new JobMatchingRequest(10, 5);
        _semanticMatchingServiceMock.Setup(x => x.RankApplicantsForJobAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateJobFitRankingBatchResult>.Success(new CandidateJobFitRankingBatchResult(targetJobId, 0, 0, 0, 0, 0, 10, 5, 0, "", null, new List<CandidateJobFitRankingResult>())));

        var result = await _sut.ExecuteMatchingAsync(targetJobId, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Statistics.CandidatePoolCount); // candidate1, candidate2, candidateShared
    }

    [Fact]
    public async Task ExecuteMatchingAsync_Idempotency_CreatesMissingAndReusesExistingCases()
    {
        var targetJobId = Guid.NewGuid();
        var positionId = Guid.NewGuid();
        _dbContext.JobRequisitions.Add(new JobRequisition { Id = targetJobId, PositionId = positionId, Title = "Target", RequisitionCode = "REQ1" });

        var sameJobId = Guid.NewGuid();
        _dbContext.JobRequisitions.Add(new JobRequisition { Id = sameJobId, PositionId = positionId, Title = "Same", RequisitionCode = "REQ2" });

        var c1 = Guid.NewGuid(); // Existing in target
        var c2 = Guid.NewGuid(); // from same pos
        _dbContext.Candidates.AddRange(new Candidate { Id = c1, CandidateCode = "C1" }, new Candidate { Id = c2, CandidateCode = "C2" });

        var existingCaseId = Guid.NewGuid();
        _dbContext.CandidateEvaluationCases.AddRange(
            new CandidateEvaluationCase { Id = existingCaseId, JobRequisitionId = targetJobId, CandidateId = c1, Status = CandidateEvaluationStatus.New },
            new CandidateEvaluationCase { Id = Guid.NewGuid(), JobRequisitionId = sameJobId, CandidateId = c2, Status = CandidateEvaluationStatus.New }
        );
        await _dbContext.SaveChangesAsync();

        var request = new JobMatchingRequest(10, 5);
        _semanticMatchingServiceMock.Setup(x => x.RankApplicantsForJobAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateJobFitRankingBatchResult>.Success(new CandidateJobFitRankingBatchResult(targetJobId, 0, 0, 0, 0, 0, 10, 5, 0, "", null, new List<CandidateJobFitRankingResult>())));

        var result1 = await _sut.ExecuteMatchingAsync(targetJobId, request, CancellationToken.None);
        var targetCases1 = await _dbContext.CandidateEvaluationCases.Where(c => c.JobRequisitionId == targetJobId).ToListAsync();
        Assert.Equal(2, targetCases1.Count);
        Assert.Contains(targetCases1, c => c.Id == existingCaseId);

        var result2 = await _sut.ExecuteMatchingAsync(targetJobId, request, CancellationToken.None);
        var targetCases2 = await _dbContext.CandidateEvaluationCases.Where(c => c.JobRequisitionId == targetJobId).ToListAsync();
        Assert.Equal(2, targetCases2.Count); // Idempotent
    }

    [Fact]
    public async Task ExecuteMatchingAsync_SourceSafety_NeverModifiesSourceJobRequisitionId()
    {
        var targetJobId = Guid.NewGuid();
        var positionId = Guid.NewGuid();
        _dbContext.JobRequisitions.Add(new JobRequisition { Id = targetJobId, PositionId = positionId, Title = "Target", RequisitionCode = "REQ1" });

        var sameJobId = Guid.NewGuid();
        _dbContext.JobRequisitions.Add(new JobRequisition { Id = sameJobId, PositionId = positionId, Title = "Same", RequisitionCode = "REQ2" });

        var c1 = Guid.NewGuid();
        _dbContext.Candidates.Add(new Candidate { Id = c1, CandidateCode = "C1" });

        var sourceCaseId = Guid.NewGuid();
        _dbContext.CandidateEvaluationCases.Add(new CandidateEvaluationCase { Id = sourceCaseId, JobRequisitionId = sameJobId, CandidateId = c1, Status = CandidateEvaluationStatus.New });
        await _dbContext.SaveChangesAsync();

        var request = new JobMatchingRequest(10, 5);
        _semanticMatchingServiceMock.Setup(x => x.RankApplicantsForJobAsync(It.IsAny<Guid>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateJobFitRankingBatchResult>.Success(new CandidateJobFitRankingBatchResult(targetJobId, 0, 0, 0, 0, 0, 10, 5, 0, "", null, new List<CandidateJobFitRankingResult>())));

        await _sut.ExecuteMatchingAsync(targetJobId, request, CancellationToken.None);

        var sourceCase = await _dbContext.CandidateEvaluationCases.SingleAsync(c => c.Id == sourceCaseId);
        Assert.Equal(sameJobId, sourceCase.JobRequisitionId); // Source unchanged
    }

    [Fact]
    public async Task ExecuteMatchingAsync_Telemetry_MapsCountsProperly()
    {
        var targetJobId = Guid.NewGuid();
        _dbContext.JobRequisitions.Add(new JobRequisition { Id = targetJobId, PositionId = Guid.NewGuid(), Title = "Target", RequisitionCode = "REQ1" });
        await _dbContext.SaveChangesAsync();

        var request = new JobMatchingRequest(20, 10);

        var prescreen = new List<CandidatePreScreeningResult>
        {
            new CandidatePreScreeningResult(Guid.NewGuid(), targetJobId,
                new SkillEvaluationResult(0, 0, 0, EvaluationStatus.NotApplicable, true, new List<Guid>(), new List<Guid>()),
                new SkillEvaluationResult(0, 0, 0, EvaluationStatus.NotApplicable, true, new List<Guid>(), new List<Guid>()),
                new ExperienceEvaluationResult(0, 0, EvaluationStatus.NotApplicable, true),
                new EducationEvaluationResult(null, null, EvaluationStatus.NotApplicable, true),
                new WorkModeEvaluationResult(null, false, true, EvaluationStatus.NotApplicable, true),
                new List<LanguageEvaluationResult>(),
                true, new List<PreScreeningFailureReason>(), 0m),
            new CandidatePreScreeningResult(Guid.NewGuid(), targetJobId,
                new SkillEvaluationResult(0, 0, 0, EvaluationStatus.NotApplicable, false, new List<Guid>(), new List<Guid>()),
                new SkillEvaluationResult(0, 0, 0, EvaluationStatus.NotApplicable, true, new List<Guid>(), new List<Guid>()),
                new ExperienceEvaluationResult(0, 0, EvaluationStatus.NotApplicable, true),
                new EducationEvaluationResult(null, null, EvaluationStatus.NotApplicable, true),
                new WorkModeEvaluationResult(null, false, true, EvaluationStatus.NotApplicable, true),
                new List<LanguageEvaluationResult>(),
                false, new List<PreScreeningFailureReason>(), 0m)
        };

        var batchResult = new CandidateJobFitRankingBatchResult(
            targetJobId,
            TotalApplicants: 100,
            PreScreeningEligibleCount: 1,
            PreScreeningRejectedCount: 1,
            RetrievedCandidateCount: 15,
            CrossEncodedCandidateCount: 8,
            RequestedRetrievalTopN: 20,
            RequestedFinalTopN: 10,
            FinalCandidateCount: 5,
            JobDocumentText: "Doc",
            PreScreeningResults: prescreen,
            Results: new List<CandidateJobFitRankingResult>()
        );

        _semanticMatchingServiceMock.Setup(x => x.RankApplicantsForJobAsync(targetJobId, request.RetrievalTopN, request.FinalTopN, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateJobFitRankingBatchResult>.Success(batchResult));

        var result = await _sut.ExecuteMatchingAsync(targetJobId, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        var stats = result.Value.Statistics;
        Assert.Equal(0, stats.CandidatePoolCount);
        Assert.Equal(0, stats.ExistingEvaluationCaseCount);
        Assert.Equal(0, stats.CreatedEvaluationCaseCount);
        Assert.Equal(2, stats.PreScreenedCandidateCount);
        Assert.Equal(1, stats.EligibleAfterHardFilters);
        Assert.Equal(1, stats.RejectedByHardFilters);
        Assert.Equal(1, stats.EmbeddingInputCount); // From eligible
        Assert.Equal(20, stats.RequestedRetrievalTopN);
        Assert.Equal(15, stats.EmbeddingRetrievedCount);
        Assert.Equal(8, stats.CrossEncoderInputCount);
        Assert.Equal(10, stats.RequestedFinalTopN);
        Assert.Equal(5, stats.FinalReturnedCount);
    }

    [Fact]
    public async Task ExecuteMatchingAsync_ZeroEligible_ReturnsZeroedTelemetry()
    {
        var targetJobId = Guid.NewGuid();
        _dbContext.JobRequisitions.Add(new JobRequisition { Id = targetJobId, PositionId = Guid.NewGuid(), Title = "Target", RequisitionCode = "REQ1" });
        await _dbContext.SaveChangesAsync();

        var request = new JobMatchingRequest(10, 5);

        var prescreen = new List<CandidatePreScreeningResult>
        {
            new CandidatePreScreeningResult(Guid.NewGuid(), targetJobId,
                new SkillEvaluationResult(0, 0, 0, EvaluationStatus.NotApplicable, false, new List<Guid>(), new List<Guid>()),
                new SkillEvaluationResult(0, 0, 0, EvaluationStatus.NotApplicable, true, new List<Guid>(), new List<Guid>()),
                new ExperienceEvaluationResult(0, 0, EvaluationStatus.NotApplicable, true),
                new EducationEvaluationResult(null, null, EvaluationStatus.NotApplicable, true),
                new WorkModeEvaluationResult(null, false, true, EvaluationStatus.NotApplicable, true),
                new List<LanguageEvaluationResult>(),
                false, new List<PreScreeningFailureReason>(), 0m)
        };

        var batchResult = new CandidateJobFitRankingBatchResult(
            targetJobId,
            TotalApplicants: 1,
            PreScreeningEligibleCount: 0,
            PreScreeningRejectedCount: 1,
            RetrievedCandidateCount: 0,
            CrossEncodedCandidateCount: 0,
            RequestedRetrievalTopN: 10,
            RequestedFinalTopN: 5,
            FinalCandidateCount: 0,
            JobDocumentText: "Doc",
            PreScreeningResults: prescreen,
            Results: new List<CandidateJobFitRankingResult>()
        );

        _semanticMatchingServiceMock.Setup(x => x.RankApplicantsForJobAsync(targetJobId, request.RetrievalTopN, request.FinalTopN, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateJobFitRankingBatchResult>.Success(batchResult));

        var result = await _sut.ExecuteMatchingAsync(targetJobId, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.Statistics.EligibleAfterHardFilters);
        Assert.Equal(0, result.Value.Statistics.EmbeddingInputCount);
        Assert.Equal(0, result.Value.Statistics.EmbeddingRetrievedCount);
        Assert.Equal(0, result.Value.Statistics.CrossEncoderInputCount);
        Assert.Equal(0, result.Value.Statistics.FinalReturnedCount);
    }

    [Fact]
    public async Task ExecuteMatchingAsync_HardFilterBreakdown_AggregatesResultsCorrectly()
    {
        var targetJobId = Guid.NewGuid();
        _dbContext.JobRequisitions.Add(new JobRequisition { Id = targetJobId, PositionId = Guid.NewGuid(), Title = "Target", RequisitionCode = "REQ1" });
        await _dbContext.SaveChangesAsync();

        var prescreen = new List<CandidatePreScreeningResult>
        {
            new CandidatePreScreeningResult(Guid.NewGuid(), targetJobId,
                new SkillEvaluationResult(1, 0, 0, EvaluationStatus.Fail, false, new List<Guid>(), new List<Guid>()), // Mandatory Fail
                new SkillEvaluationResult(0, 0, 0, EvaluationStatus.NotApplicable, true, new List<Guid>(), new List<Guid>()), // Pref
                new ExperienceEvaluationResult(10, 20, EvaluationStatus.Pass, true), // Exp Pass
                new EducationEvaluationResult(null, null, EvaluationStatus.NotApplicable, true), // Ed NA
                new WorkModeEvaluationResult(Guid.NewGuid(), true, false, EvaluationStatus.Fail, false), // WorkMode Fail
                new List<LanguageEvaluationResult>(),
                false, new List<PreScreeningFailureReason>(), 0m),
            new CandidatePreScreeningResult(Guid.NewGuid(), targetJobId,
                new SkillEvaluationResult(1, 1, 1, EvaluationStatus.Pass, true, new List<Guid>(), new List<Guid>()), // Mandatory Pass
                new SkillEvaluationResult(0, 0, 0, EvaluationStatus.NotApplicable, true, new List<Guid>(), new List<Guid>()), // Pref
                new ExperienceEvaluationResult(10, 20, EvaluationStatus.Fail, false), // Exp Fail
                new EducationEvaluationResult(null, null, EvaluationStatus.NotApplicable, true), // Ed NA
                new WorkModeEvaluationResult(null, false, true, EvaluationStatus.NotApplicable, true), // WorkMode NA
                new List<LanguageEvaluationResult> { new LanguageEvaluationResult(Guid.NewGuid(), HrDecisionSupport.Domain.Enums.LanguageProficiencyLevel.B2, HrDecisionSupport.Domain.Enums.LanguageProficiencyLevel.B2, true, EvaluationStatus.Pass, true) }, // Lang Pass
                false, new List<PreScreeningFailureReason>(), 0m)
        };

        var batchResult = new CandidateJobFitRankingBatchResult(
            targetJobId, 2, 0, 2, 0, 0, 10, 5, 0, "", prescreen, new List<CandidateJobFitRankingResult>());

        _semanticMatchingServiceMock.Setup(x => x.RankApplicantsForJobAsync(targetJobId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateJobFitRankingBatchResult>.Success(batchResult));

        var result = await _sut.ExecuteMatchingAsync(targetJobId, new JobMatchingRequest(10, 5), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var breakdown = result.Value.HardFilterBreakdown;
        Assert.NotNull(breakdown);
        Assert.Equal(1, breakdown.MandatorySkill.PassCount);
        Assert.Equal(1, breakdown.MandatorySkill.FailCount);
        Assert.Equal(1, breakdown.Experience.PassCount);
        Assert.Equal(1, breakdown.Experience.FailCount);
        Assert.Equal(0, breakdown.Education.PassCount);
        Assert.Equal(2, breakdown.Education.NotApplicableCount);
        Assert.Equal(0, breakdown.WorkMode.PassCount);
        Assert.Equal(1, breakdown.WorkMode.FailCount);
        Assert.Equal(1, breakdown.WorkMode.NotApplicableCount);
        Assert.Equal(1, breakdown.Language.PassCount);
        Assert.Equal(0, breakdown.Language.FailCount);
        Assert.Equal(1, breakdown.Language.NotApplicableCount);

        // Sum of fail counts > RejectedByHardFilters
        Assert.Equal(2, result.Value.Statistics.RejectedByHardFilters);
        Assert.True((breakdown.MandatorySkill.FailCount + breakdown.Experience.FailCount + breakdown.WorkMode.FailCount) > 2);
    }

    [Fact]
    public async Task ExecuteMatchingAsync_TopNBehavior_ReturnsCorrectFinalTopN()
    {
        var targetJobId = Guid.NewGuid();
        _dbContext.JobRequisitions.Add(new JobRequisition { Id = targetJobId, PositionId = Guid.NewGuid(), Title = "Target", RequisitionCode = "REQ1" });
        await _dbContext.SaveChangesAsync();

        var prescreen = new List<CandidatePreScreeningResult>();

        var mockResults = new List<CandidateJobFitRankingResult>();
        for(int i = 0; i < 3; i++) {
            mockResults.Add(new CandidateJobFitRankingResult(Guid.NewGuid(), i + 1, 1, i + 1, 0.9, 0.9, 0.9, "Doc", 1, 1, 1m, 0, 0, 1m));
        }

        var batchResult = new CandidateJobFitRankingBatchResult(
            targetJobId, 20, 20, 0, 7, 7, 7, 3, 3, "", prescreen, mockResults);

        _semanticMatchingServiceMock.Setup(x => x.RankApplicantsForJobAsync(targetJobId, 7, 3, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateJobFitRankingBatchResult>.Success(batchResult));

        var result = await _sut.ExecuteMatchingAsync(targetJobId, new JobMatchingRequest(7, 3), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(7, result.Value.Statistics.CrossEncoderInputCount);
        Assert.Equal(3, result.Value.Statistics.FinalReturnedCount);
        Assert.Equal(3, result.Value.Candidates.Count);
    }
}
