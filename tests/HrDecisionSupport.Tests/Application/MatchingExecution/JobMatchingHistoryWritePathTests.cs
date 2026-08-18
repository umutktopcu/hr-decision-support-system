using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.MatchingExecution;
using HrDecisionSupport.Application.MatchingExecution.History;
using HrDecisionSupport.Application.MatchingExecution.Models;
using HrDecisionSupport.Application.SemanticMatching.Reranking;
using HrDecisionSupport.Application.SemanticMatching.Reranking.Models;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.MatchingHistory;
using HrDecisionSupport.Infrastructure.Persistence;
using HrDecisionSupport.Infrastructure.SemanticMatching;
using HrDecisionSupport.Infrastructure.SemanticMatching.Reranking;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace HrDecisionSupport.Tests.Application.MatchingExecution;

public sealed class JobMatchingHistoryWritePathTests
{
    [Fact]
    public async Task ExecuteMatching_WithRealWriterPersistsExactlyOneRunAndAllFinalResults()
    {
        await using var context = CreateContext();
        var job = SeedConfiguredJob(context);
        var first = SeedCandidate(context, "C1", "First", "Saved", null, null);
        var second = SeedCandidate(context, "C2", "Second", "Saved", null, null);
        await context.SaveChangesAsync();
        var ranking = RankingMock(
            job.Id,
            [RankingResult(first.Id, 1, 1, 0.9), RankingResult(second.Id, 2, 2, 0.8)],
            retrievedCount: 2,
            finalTopN: 2);
        var service = CreateExecutionService(
            context,
            ranking.Object,
            Mock.Of<IMlPredictionService>(),
            CreateHistoryService(context));

        var result = await service.ExecuteMatchingAsync(job.Id, new JobMatchingRequest(10, 2));

        Assert.True(result.IsSuccess);
        var run = await context.JobMatchingRuns.Include(item => item.Results).SingleAsync();
        Assert.Equal(2, run.FinalCandidateCount);
        Assert.Equal(2, run.Results.Count);
        Assert.Equal(result.Value.Candidates.Select(item => item.FinalRank),
            run.Results.OrderBy(item => item.FinalRank).Select(item => item.FinalRank));
        Assert.Equal(result.Value.Candidates.Select(item => item.JobFitScore),
            run.Results.OrderBy(item => item.FinalRank).Select(item => item.JobFitScore));
    }

    [Fact]
    public async Task HistoryWriter_PersistsExactRunResultMetadataConfigurationAndHash()
    {
        await using var context = CreateContext();
        var job = SeedConfiguredJob(context);
        var candidate = SeedCandidate(context, "C-001", "Ada", "Lovelace", 6, 24);
        await context.SaveChangesAsync();
        var exactDocument = "JOB PROFILE\r\nTitle: Exact  \n";
        var completed = CompletedRun(job.Id, exactDocument,
        [
            CompletedResult(candidate.Id, "C-001", "Ada Lovelace", 1, 2,
                RetentionPredictionStatus.Predicted, EmployeeRetentionLabelValue.Long, 6, 24)
        ]);
        var service = CreateHistoryService(context);

        await service.SaveCompletedRunAsync(completed);

        var run = await context.JobMatchingRuns.Include(item => item.Results).SingleAsync();
        var saved = Assert.Single(run.Results);
        Assert.Equal(job.Id, run.JobRequisitionId);
        Assert.Equal("REQ-HISTORY", run.JobRequisitionCodeSnapshot);
        Assert.Equal("Backend Engineer", run.JobTitleSnapshot);
        Assert.Equal(10, run.RetrievalTopN);
        Assert.Equal(1, run.FinalTopN);
        Assert.Equal(7, run.CandidatePoolCount);
        Assert.Equal(5, run.HardFilterPassedCount);
        Assert.Equal(3, run.RetrievedCandidateCount);
        Assert.Equal(1, run.FinalCandidateCount);
        Assert.Equal("Qwen/Qwen3-Embedding-0.6B", run.EmbeddingModelName);
        Assert.Equal("Qwen/Qwen3-Reranker-0.6B", run.RerankerModelName);
        Assert.Equal("retention-rf-v1", run.RetentionModelName);
        Assert.Equal("retention-features-v1", run.RetentionFeatureSchemaVersion);
        Assert.Equal(Hash(exactDocument), run.JobDocumentHash);

        Assert.Equal(candidate.Id, saved.CandidateId);
        Assert.Equal("C-001", saved.CandidateCodeSnapshot);
        Assert.Equal("Ada Lovelace", saved.CandidateDisplayNameSnapshot);
        Assert.Equal(1, saved.FinalRank);
        Assert.Equal(2, saved.SkillTier);
        Assert.Equal(0.75m, saved.MandatorySkillCoverage);
        Assert.Equal(0.50m, saved.PreferredSkillCoverage);
        Assert.Equal(0.81, saved.EmbeddingScore);
        Assert.Equal(1.23, saved.CrossEncoderRawScore);
        Assert.Equal(0.77, saved.JobFitScore);
        Assert.Equal(RetentionPredictionStatus.Predicted, saved.RetentionPredictionStatus);
        Assert.Equal(EmployeeRetentionLabelValue.Long, saved.RetentionLabel);
        Assert.Equal(6, saved.ShortestPreviousJobMonthsSnapshot);
        Assert.Equal(24, saved.LongestPreviousJobMonthsSnapshot);

        using var json = JsonDocument.Parse(run.ConfigurationSnapshotJson);
        var root = json.RootElement;
        Assert.Equal("job-matching-configuration-v1", root.GetProperty("snapshotSchemaVersion").GetString());
        Assert.Equal("Backend Engineer", root.GetProperty("jobTitle").GetString());
        Assert.Equal("Build APIs", root.GetProperty("jobDescription").GetString());
        Assert.Equal("Backend Developer", root.GetProperty("position").GetProperty("name").GetString());
        Assert.Equal(18, root.GetProperty("minimumRelevantExperienceMonths").GetInt32());
        Assert.Equal("Bachelor", root.GetProperty("minimumEducation").GetString());
        Assert.Equal(0.75m, root.GetProperty("mandatorySkillCoverageThreshold").GetDecimal());
        Assert.Equal("Remote", root.GetProperty("workMode").GetProperty("name").GetString());
        Assert.True(root.GetProperty("workModeHardFilterEnabled").GetBoolean());
        var mandatoryCompetency = root.GetProperty("mandatoryCompetencies")[0];
        Assert.Equal("C#", mandatoryCompetency.GetProperty("name").GetString());
        Assert.False(mandatoryCompetency.TryGetProperty("minimumExperienceMonths", out _));
        Assert.False(mandatoryCompetency.TryGetProperty("minimumProficiencyLevel", out _));
        Assert.Equal("PostgreSQL", root.GetProperty("preferredCompetencies")[0].GetProperty("name").GetString());
        Assert.Equal("English", root.GetProperty("languageRequirements")[0].GetProperty("name").GetString());
        Assert.True(root.GetProperty("languageRequirements")[0].GetProperty("hardFilterEnabled").GetBoolean());

        candidate.CandidateCode = "CHANGED";
        candidate.Person.FirstName = "Changed";
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        saved = await context.JobMatchingRunResults.SingleAsync();
        Assert.Equal("C-001", saved.CandidateCodeSnapshot);
        Assert.Equal("Ada Lovelace", saved.CandidateDisplayNameSnapshot);
    }

    [Fact]
    public async Task ExecuteMatching_PredictsOnlyFinalCandidatesAndPreservesFinalOrder()
    {
        await using var context = CreateContext();
        var job = SeedMinimalJob(context);
        var first = SeedCandidate(context, "C1", "First", "Candidate", 3, 12);
        var second = SeedCandidate(context, "C2", "Second", "Candidate", 6, 24);
        var third = SeedCandidate(context, "C3", "Third", "Candidate", 9, 36);
        SeedEvaluationCases(context, job.Id, first.Id, second.Id, third.Id);
        await context.SaveChangesAsync();

        var ordered = new[]
        {
            RankingResult(first.Id, 1, 1, 0.91),
            RankingResult(second.Id, 2, 2, 0.82),
            RankingResult(third.Id, 3, 3, 0.73)
        };
        var ranking = RankingMock(job.Id, ordered, retrievedCount: 10, finalTopN: 3);
        var retention = new Mock<IMlPredictionService>();
        retention.Setup(service => service.PredictStayAsync(
                It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((double shortest, double _, CancellationToken _) => shortest switch
            {
                3 => 2,
                6 => 0,
                _ => 1
            });
        CompletedJobMatchingRun? persisted = null;
        var history = CapturingHistory(run => persisted = run);
        var service = CreateExecutionService(context, ranking.Object, retention.Object, history.Object);

        var result = await service.ExecuteMatchingAsync(job.Id, new JobMatchingRequest(10, 3));

        Assert.True(result.IsSuccess);
        Assert.Equal(ordered.Select(item => item.CandidateId), result.Value.Candidates.Select(item => item.CandidateId));
        Assert.Equal([1, 2, 3], result.Value.Candidates.Select(item => item.FinalRank));
        Assert.Equal(
            [EmployeeRetentionLabelValue.Long, EmployeeRetentionLabelValue.Short, EmployeeRetentionLabelValue.Normal],
            result.Value.Candidates.Select(item => item.RetentionLabel));
        retention.Verify(service => service.PredictStayAsync(
            It.IsAny<double>(), It.IsAny<double>(), It.IsAny<CancellationToken>()), Times.Exactly(3));
        Assert.NotNull(persisted);
        Assert.Equal(3, persisted.Results.Count);
        Assert.Equal(result.Value.Candidates.Select(item => item.CandidateId), persisted.Results.Select(item => item.CandidateId));
        Assert.Equal(result.Value.Candidates.Select(item => item.RetentionLabel), persisted.Results.Select(item => item.RetentionLabel));
        Assert.Equal(10, persisted.RetrievedCandidateCount);
    }

    [Theory]
    [InlineData(null, 12)]
    [InlineData(0, 12)]
    [InlineData(3, 0)]
    public async Task ExecuteMatching_InvalidRetentionInputs_AreInsufficientWithoutMlCall(
        int? shortest,
        int? longest)
    {
        await using var context = CreateContext();
        var job = SeedMinimalJob(context);
        var candidate = SeedCandidate(context, "C1", "No", "Data", shortest, longest);
        await context.SaveChangesAsync();
        var ranking = RankingMock(job.Id, [RankingResult(candidate.Id, 1, 1, 0.8)], 1, 1);
        var retention = new Mock<IMlPredictionService>(MockBehavior.Strict);
        CompletedJobMatchingRun? persisted = null;
        var history = CapturingHistory(run => persisted = run);
        var service = CreateExecutionService(context, ranking.Object, retention.Object, history.Object);

        var result = await service.ExecuteMatchingAsync(job.Id, new JobMatchingRequest(10, 1));

        Assert.True(result.IsSuccess);
        var live = Assert.Single(result.Value.Candidates);
        Assert.Equal(RetentionPredictionStatus.InsufficientData, live.RetentionPredictionStatus);
        Assert.Null(live.RetentionLabel);
        var saved = Assert.Single(persisted!.Results);
        Assert.Equal(shortest, saved.ShortestPreviousJobMonthsSnapshot);
        Assert.Equal(longest, saved.LongestPreviousJobMonthsSnapshot);
        Assert.Equal(RetentionPredictionStatus.InsufficientData, saved.RetentionPredictionStatus);
        Assert.Null(saved.RetentionLabel);
        retention.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteMatching_RetentionFailureMarksUnavailableWithoutChangingRank()
    {
        await using var context = CreateContext();
        var job = SeedMinimalJob(context);
        var candidate = SeedCandidate(context, "C1", "Service", "Down", 4, 16);
        await context.SaveChangesAsync();
        var ranking = RankingMock(job.Id, [RankingResult(candidate.Id, 1, 2, 0.8)], 1, 1);
        var retention = new Mock<IMlPredictionService>();
        retention.Setup(service => service.PredictStayAsync(4, 16, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("offline"));
        CompletedJobMatchingRun? persisted = null;
        var history = CapturingHistory(run => persisted = run);
        var service = CreateExecutionService(context, ranking.Object, retention.Object, history.Object);

        var result = await service.ExecuteMatchingAsync(job.Id, new JobMatchingRequest(10, 1));

        Assert.True(result.IsSuccess);
        var live = Assert.Single(result.Value.Candidates);
        Assert.Equal(1, live.FinalRank);
        Assert.Equal(2, live.SkillTier);
        Assert.Equal(0.8, live.JobFitScore);
        Assert.Equal(RetentionPredictionStatus.Unavailable, live.RetentionPredictionStatus);
        Assert.Null(live.RetentionLabel);
        Assert.Equal(RetentionPredictionStatus.Unavailable, Assert.Single(persisted!.Results).RetentionPredictionStatus);
    }

    [Fact]
    public async Task ExecuteMatching_ZeroResultsStillPersistsCompletedRun()
    {
        await using var context = CreateContext();
        var job = SeedMinimalJob(context);
        await context.SaveChangesAsync();
        var ranking = RankingMock(job.Id, [], 0, 5);
        CompletedJobMatchingRun? persisted = null;
        var history = CapturingHistory(run => persisted = run);
        var service = CreateExecutionService(
            context,
            ranking.Object,
            Mock.Of<IMlPredictionService>(),
            history.Object);

        var result = await service.ExecuteMatchingAsync(job.Id, new JobMatchingRequest(10, 5));

        Assert.True(result.IsSuccess);
        Assert.NotNull(persisted);
        Assert.Equal(0, persisted.FinalCandidateCount);
        Assert.Empty(persisted.Results);
    }

    [Fact]
    public async Task ExecuteMatching_RankingFailureDoesNotPersistCompletedRun()
    {
        await using var context = CreateContext();
        var job = SeedMinimalJob(context);
        await context.SaveChangesAsync();
        var ranking = new Mock<ICandidateJobFitRankingService>();
        ranking.Setup(service => service.RankApplicantsForJobAsync(
                job.Id, 10, 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateJobFitRankingBatchResult>.Failure(
                new Error("reranker_failed", "failed", ErrorType.Failure)));
        var history = new Mock<IJobMatchingHistoryService>(MockBehavior.Strict);
        var service = CreateExecutionService(
            context,
            ranking.Object,
            Mock.Of<IMlPredictionService>(),
            history.Object);

        var result = await service.ExecuteMatchingAsync(job.Id, new JobMatchingRequest(10, 5));

        Assert.True(result.IsFailure);
        history.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteMatching_HistoryFailureNeverReturnsSuccess()
    {
        await using var context = CreateContext();
        var job = SeedMinimalJob(context);
        await context.SaveChangesAsync();
        var ranking = RankingMock(job.Id, [], 0, 5);
        var history = new Mock<IJobMatchingHistoryService>();
        history.Setup(service => service.SaveCompletedRunAsync(
                It.IsAny<CompletedJobMatchingRun>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("write failed"));
        var service = CreateExecutionService(
            context,
            ranking.Object,
            Mock.Of<IMlPredictionService>(),
            history.Object);

        var result = await service.ExecuteMatchingAsync(job.Id, new JobMatchingRequest(10, 5));

        Assert.True(result.IsFailure);
        Assert.Equal("matching_history_persistence_failed", result.Error!.Code);
        Assert.Empty(await context.JobMatchingRuns.ToListAsync());
    }

    [Fact]
    public async Task HistoryWriter_FailedSaveLeavesNoPartialRunOrResults()
    {
        var databaseName = $"history-atomic-{Guid.NewGuid():N}";
        var options = new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        Guid jobId;
        await using (var seed = new HrDecisionSupportDbContext(options))
        {
            jobId = SeedConfiguredJob(seed).Id;
            await seed.SaveChangesAsync();
        }
        await using (var failing = new FailingSaveDbContext(options))
        {
            var service = CreateHistoryService(failing);
            var completed = CompletedRun(jobId, "document",
            [
                CompletedResult(Guid.NewGuid(), "C1", "One", 1, 1,
                    RetentionPredictionStatus.InsufficientData, null, null, null),
                CompletedResult(Guid.NewGuid(), "C2", "Two", 2, 1,
                    RetentionPredictionStatus.InsufficientData, null, null, null)
            ]);

            await Assert.ThrowsAsync<DbUpdateException>(() => service.SaveCompletedRunAsync(completed));
        }
        await using var verify = new HrDecisionSupportDbContext(options);
        Assert.Empty(await verify.JobMatchingRuns.ToListAsync());
        Assert.Empty(await verify.JobMatchingRunResults.ToListAsync());
    }

    private static JobMatchingExecutionService CreateExecutionService(
        HrDecisionSupportDbContext context,
        ICandidateJobFitRankingService ranking,
        IMlPredictionService retention,
        IJobMatchingHistoryService history)
    {
        var validator = new Mock<IValidator<JobMatchingRequest>>();
        validator.Setup(item => item.Validate(It.IsAny<JobMatchingRequest>()))
            .Returns(ValidationResult.Valid());
        return new(context, ranking, validator.Object, retention, history);
    }

    private static Mock<ICandidateJobFitRankingService> RankingMock(
        Guid jobId,
        IReadOnlyList<CandidateJobFitRankingResult> results,
        int retrievedCount,
        int finalTopN)
    {
        var mock = new Mock<ICandidateJobFitRankingService>();
        mock.Setup(service => service.RankApplicantsForJobAsync(
                jobId, It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidateJobFitRankingBatchResult>.Success(new(
                jobId,
                TotalApplicants: results.Count,
                PreScreeningEligibleCount: results.Count,
                PreScreeningRejectedCount: 0,
                RetrievedCandidateCount: retrievedCount,
                CrossEncodedCandidateCount: retrievedCount,
                RequestedRetrievalTopN: 10,
                RequestedFinalTopN: finalTopN,
                FinalCandidateCount: results.Count,
                JobDocumentText: "exact job document",
                PreScreeningResults: [],
                Results: results)));
        return mock;
    }

    private static Mock<IJobMatchingHistoryService> CapturingHistory(
        Action<CompletedJobMatchingRun> capture)
    {
        var mock = new Mock<IJobMatchingHistoryService>();
        mock.Setup(service => service.SaveCompletedRunAsync(
                It.IsAny<CompletedJobMatchingRun>(), It.IsAny<CancellationToken>()))
            .Callback<CompletedJobMatchingRun, CancellationToken>((run, _) => capture(run))
            .Returns(Task.CompletedTask);
        return mock;
    }

    private static CandidateJobFitRankingResult RankingResult(
        Guid candidateId,
        int rank,
        int tier,
        double jobFitScore) =>
        new(
            candidateId,
            rank,
            tier,
            rank,
            0.81 - rank / 100d,
            1.20 - rank / 100d,
            jobFitScore,
            "candidate document",
            3,
            4,
            0.75m,
            1,
            2,
            0.50m);

    private static CompletedJobMatchingRun CompletedRun(
        Guid jobId,
        string document,
        IReadOnlyList<CompletedJobMatchingResult> results) =>
        new(jobId, 10, results.Count == 0 ? 5 : results.Count, 7, 5, 3, results.Count, document, results);

    private static CompletedJobMatchingResult CompletedResult(
        Guid candidateId,
        string? code,
        string name,
        int rank,
        int tier,
        RetentionPredictionStatus status,
        EmployeeRetentionLabelValue? label,
        int? shortest,
        int? longest) =>
        new(candidateId, code, name, rank, tier, 0.75m, 0.50m, 0.81, 1.23, 0.77,
            status, label, shortest, longest);

    private static PostgreSqlJobMatchingHistoryService CreateHistoryService(
        HrDecisionSupportDbContext context) =>
        new(
            context,
            Options.Create(new QwenEmbeddingOptions
            {
                ModelName = "Qwen/Qwen3-Embedding-0.6B"
            }),
            Options.Create(new QwenRerankerOptions
            {
                ModelName = "Qwen/Qwen3-Reranker-0.6B"
            }),
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 18, 12, 0, 0, TimeSpan.Zero)));

    private static HrDecisionSupportDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseInMemoryDatabase($"history-{Guid.NewGuid():N}")
            .Options);

    private static JobRequisition SeedMinimalJob(HrDecisionSupportDbContext context)
    {
        var job = new JobRequisition
        {
            Id = Guid.NewGuid(),
            RequisitionCode = "REQ-MIN",
            Title = "Minimal",
            PositionId = Guid.NewGuid()
        };
        context.JobRequisitions.Add(job);
        return job;
    }

    private static JobRequisition SeedConfiguredJob(HrDecisionSupportDbContext context)
    {
        var position = new Position
        {
            Id = Guid.NewGuid(), Code = "BACKEND_DEVELOPER", Name = "Backend Developer"
        };
        var workMode = new WorkMode
        {
            Id = Guid.NewGuid(), Code = "REMOTE", Name = "Remote"
        };
        var mandatory = new Competency
        {
            Id = Guid.NewGuid(), Code = "CSHARP", Name = "C#"
        };
        var preferred = new Competency
        {
            Id = Guid.NewGuid(), Code = "POSTGRES", Name = "PostgreSQL"
        };
        var language = new Language
        {
            Id = Guid.NewGuid(), Code = "EN", Name = "English"
        };
        var job = new JobRequisition
        {
            Id = Guid.NewGuid(),
            RequisitionCode = "REQ-HISTORY",
            Title = "Backend Engineer",
            Description = "Build APIs",
            PositionId = position.Id,
            Position = position,
            MinimumRelevantExperienceMonths = 18,
            MinimumEducationLevel = DegreeLevel.Bachelor,
            MandatorySkillCoverageThreshold = 0.75m,
            WorkModeId = workMode.Id,
            WorkMode = workMode,
            WorkModeHardFilterEnabled = true,
            Requirements =
            [
                new JobRequisitionRequirement
                {
                    Id = Guid.NewGuid(), CompetencyId = mandatory.Id, Competency = mandatory,
                    IsRequired = true, MinimumExperienceMonths = 12,
                    MinimumProficiencyLevel = CompetencyProficiencyLevel.Advanced
                },
                new JobRequisitionRequirement
                {
                    Id = Guid.NewGuid(), CompetencyId = preferred.Id, Competency = preferred,
                    IsRequired = false
                }
            ],
            LanguageRequirements =
            [
                new JobLanguageRequirement
                {
                    Id = Guid.NewGuid(), LanguageId = language.Id, Language = language,
                    MinimumProficiency = LanguageProficiencyLevel.B2,
                    HardFilterEnabled = true
                }
            ]
        };
        context.AddRange(position, workMode, mandatory, preferred, language, job);
        return job;
    }

    private static Candidate SeedCandidate(
        HrDecisionSupportDbContext context,
        string code,
        string firstName,
        string lastName,
        int? shortest,
        int? longest)
    {
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            CandidateCode = code,
            Person = new Person
            {
                Id = Guid.NewGuid(),
                AnonymousCode = $"ANON-{code}",
                FirstName = firstName,
                LastName = lastName
            }
        };
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            FeatureSchemaVersion = "candidate-features-v1",
            CalculatedAtUtc = DateTime.UtcNow,
            ShortestPreviousJobMonths = shortest,
            LongestPreviousJobMonths = longest
        });
        context.Candidates.Add(candidate);
        return candidate;
    }

    private static void SeedEvaluationCases(
        HrDecisionSupportDbContext context,
        Guid jobId,
        params Guid[] candidateIds)
    {
        context.CandidateEvaluationCases.AddRange(candidateIds.Select(candidateId =>
            new CandidateEvaluationCase
            {
                Id = Guid.NewGuid(),
                JobRequisitionId = jobId,
                CandidateId = candidateId,
                Status = CandidateEvaluationStatus.New
            }));
    }

    private static string Hash(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class FailingSaveDbContext(
        DbContextOptions<HrDecisionSupportDbContext> options)
        : HrDecisionSupportDbContext(options)
    {
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("simulated atomic write failure");
    }
}
