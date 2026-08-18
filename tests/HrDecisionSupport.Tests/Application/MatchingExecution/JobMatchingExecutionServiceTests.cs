using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.MatchingExecution;
using HrDecisionSupport.Application.MatchingExecution.History;
using HrDecisionSupport.Application.MatchingExecution.Models;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.SemanticMatching.Orchestration;
using HrDecisionSupport.Application.SemanticMatching.Orchestration.Models;
using HrDecisionSupport.Application.SemanticMatching.Reranking;
using HrDecisionSupport.Application.SemanticMatching.Reranking.Models;
using HrDecisionSupport.Application.PreScreening.Models;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using HrDecisionSupport.Domain.Enums;
using System.Linq;

namespace HrDecisionSupport.Tests.Application.MatchingExecution;

public class JobMatchingExecutionServiceTests
{
    private readonly HrDecisionSupportDbContext _dbContext;
    private readonly Mock<ICandidateJobFitRankingService> _semanticMatchingServiceMock;
    private readonly Mock<IValidator<JobMatchingRequest>> _validatorMock;
    private readonly Mock<IMlPredictionService> _retentionPredictionServiceMock;
    private readonly Mock<IJobMatchingHistoryService> _historyServiceMock;
    private readonly JobMatchingExecutionService _sut;

    public JobMatchingExecutionServiceTests()
    {
        var options = new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new HrDecisionSupportDbContext(options);

        _semanticMatchingServiceMock = new Mock<ICandidateJobFitRankingService>();
        _validatorMock = new Mock<IValidator<JobMatchingRequest>>();
        _retentionPredictionServiceMock = new Mock<IMlPredictionService>();
        _historyServiceMock = new Mock<IJobMatchingHistoryService>();
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
    public async Task ExecuteMatchingAsync_ValidationFails_ReturnsValidationFailure()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var request = new JobMatchingRequest(10, 5);
        _validatorMock.Setup(x => x.Validate(request))
            .Returns(ValidationResult.Invalid(new ValidationError("RetrievalTopN", "Error", "RetrievalTopN")));

        // Act
        var result = await _sut.ExecuteMatchingAsync(jobId, request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("RetrievalTopN", result.Error.Code);
    }

    [Fact]
    public async Task ExecuteMatchingAsync_JobNotFound_ReturnsNotFound()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var request = new JobMatchingRequest(10, 5);
        _validatorMock.Setup(x => x.Validate(request)).Returns(ValidationResult.Valid());

        // Act
        var result = await _sut.ExecuteMatchingAsync(jobId, request, CancellationToken.None);

        // Assert
        Assert.True(result.IsFailure);
        Assert.Equal("job_requisition_not_found", result.Error.Code);
    }

    [Fact]
    public async Task ExecuteMatchingAsync_ValidRequest_OrchestratesCorrectly()
    {
        // Arrange
        var jobId = Guid.NewGuid();
        var positionId = Guid.NewGuid();
        var request = new JobMatchingRequest(10, 5);
        _validatorMock.Setup(x => x.Validate(request)).Returns(ValidationResult.Valid());

        var job = new JobRequisition {
            Id = jobId,
            PositionId = positionId,
            Title = "Test",
            RequisitionCode = "TEST-01"
        };
        _dbContext.JobRequisitions.Add(job);

        var candidate1 = Guid.NewGuid();
        var candidate2 = Guid.NewGuid();
        _dbContext.Candidates.AddRange(
            new Candidate { Id = candidate1, CandidateCode = "C1", Person = new Person { Id = Guid.NewGuid(), FirstName = "A", LastName = "B", AnonymousCode = "A1" } },
            new Candidate { Id = candidate2, CandidateCode = "C2", Person = new Person { Id = Guid.NewGuid(), FirstName = "C", LastName = "D", AnonymousCode = "A2" } }
        );

        var prevJobId = Guid.NewGuid();
        var prevJob = new JobRequisition
        {
            Id = prevJobId,
            RequisitionCode = "TEST-PREV",
            Title = "Backend Dev Prev",
            PositionId = positionId,
            CreatedAtUtc = DateTime.UtcNow,
            OpeningsCount = 1,
            JobRequisitionStatus = JobRequisitionStatus.Closed
        };
        _dbContext.JobRequisitions.Add(prevJob);

        _dbContext.CandidateEvaluationCases.AddRange(
            new CandidateEvaluationCase { Id = Guid.NewGuid(), JobRequisitionId = prevJobId, CandidateId = candidate1, Status = CandidateEvaluationStatus.New },
            new CandidateEvaluationCase { Id = Guid.NewGuid(), JobRequisitionId = prevJobId, CandidateId = candidate2, Status = CandidateEvaluationStatus.New }
        );
        await _dbContext.SaveChangesAsync();

        var matchingResult = Result<CandidateJobFitRankingBatchResult>.Success(
            new CandidateJobFitRankingBatchResult(
                jobId,
                2,
                2,
                0,
                2,
                2,
                10,
                5,
                2,
                "Test",
                Array.Empty<CandidatePreScreeningResult>(),
                new List<CandidateJobFitRankingResult>
                {
                    new CandidateJobFitRankingResult(candidate1, 1, 1, 1, 0.9, 0.9, 0.9, "Doc", 1, 1, 1.0m, 1, 1, 1.0m),
                    new CandidateJobFitRankingResult(candidate2, 2, 1, 2, 0.8, 0.8, 0.8, "Doc", 1, 1, 1.0m, 1, 1, 1.0m)
                }
            )
        );

        _semanticMatchingServiceMock.Setup(x => x.RankApplicantsForJobAsync(jobId, request.RetrievalTopN, request.FinalTopN, It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchingResult);

        // Act
        var result = await _sut.ExecuteMatchingAsync(jobId, request, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        System.Console.WriteLine($"Candidates: {result.Value.Candidates.Count}, Pool: {result.Value.Statistics.CandidatePoolCount}");
        Assert.Equal(2, result.Value.Candidates.Count);
        Assert.Equal(2, result.Value.Statistics.CandidatePoolCount);

        var cases = await _dbContext.CandidateEvaluationCases.Where(c => c.JobRequisitionId == jobId).ToListAsync();
        Assert.Equal(2, cases.Count);
    }
}
