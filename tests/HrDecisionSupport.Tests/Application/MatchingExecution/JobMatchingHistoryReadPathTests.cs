using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.MatchingExecution.History;
using HrDecisionSupport.Application.SemanticMatching.Embeddings;
using HrDecisionSupport.Application.SemanticMatching.Reranking;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.MatchingHistory;
using HrDecisionSupport.Infrastructure.Persistence;
using HrDecisionSupport.Infrastructure.SemanticMatching;
using HrDecisionSupport.Infrastructure.SemanticMatching.Reranking;
using HrDecisionSupport.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace HrDecisionSupport.Tests.Application.MatchingExecution;

public sealed class JobMatchingHistoryReadPathTests
{
    private const string ValidConfigurationJson = """
        {
          "snapshotSchemaVersion": "job-matching-configuration-v1",
          "jobTitle": "Stored Job",
          "jobDescription": "Stored description",
          "position": { "id": "11111111-1111-1111-1111-111111111111", "code": "BACKEND", "name": "Stored Position" },
          "minimumRelevantExperienceMonths": 18,
          "minimumEducation": "Bachelor",
          "mandatorySkillCoverageThreshold": 0.75,
          "workMode": { "id": "22222222-2222-2222-2222-222222222222", "code": "REMOTE", "name": "Stored Remote" },
          "workModeHardFilterEnabled": true,
          "mandatoryCompetencies": [
            { "competencyId": "33333333-3333-3333-3333-333333333333", "code": "CSHARP", "name": "Stored C#" }
          ],
          "preferredCompetencies": [
            { "competencyId": "44444444-4444-4444-4444-444444444444", "code": "POSTGRES", "name": "Stored PostgreSQL" }
          ],
          "languageRequirements": [
            { "languageId": "55555555-5555-5555-5555-555555555555", "code": "EN", "name": "Stored English", "minimumProficiency": "B2", "hardFilterEnabled": true }
          ]
        }
        """;

    [Fact]
    public async Task GetRunsForJobAsync_ReturnsOnlyRequestedJobNewestFirstWithoutLoadingResults()
    {
        await using var context = CreateContext();
        var jobA = SeedJob(context, "JOB-A", "Job A");
        var jobB = SeedJob(context, "JOB-B", "Job B");
        var older = SeedRun(context, jobA.Id, new DateTime(2026, 8, 17, 8, 0, 0, DateTimeKind.Utc));
        var newer = SeedRun(context, jobA.Id, new DateTime(2026, 8, 18, 8, 0, 0, DateTimeKind.Utc));
        SeedRun(context, jobB.Id, new DateTime(2026, 8, 19, 8, 0, 0, DateTimeKind.Utc));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var runs = await CreateService(context).GetRunsForJobAsync(jobA.Id);

        Assert.Equal([newer.Id, older.Id], runs.Select(run => run.RunId));
        Assert.All(runs, run => Assert.Equal("STORED-CODE", run.JobRequisitionCodeSnapshot));
        Assert.Empty(context.ChangeTracker.Entries<JobMatchingRunResult>());
        Assert.Empty(context.ChangeTracker.Entries<JobMatchingRun>());
    }

    [Fact]
    public async Task GetRunDetailAsync_ReturnsExactSnapshotWithCandidatesOrderedByFinalRank()
    {
        await using var context = CreateContext();
        var job = SeedJob(context, "CURRENT", "Current Job");
        var run = SeedRun(context, job.Id, DateTime.SpecifyKind(new DateTime(2026, 8, 18, 12, 0, 0), DateTimeKind.Utc));
        run.Results =
        [
            Result(run.Id, 5, RetentionPredictionStatus.Unavailable, null, "C5", "Unavailable"),
            Result(run.Id, 2, RetentionPredictionStatus.Predicted, EmployeeRetentionLabelValue.Normal, "C2", "Normal"),
            Result(run.Id, 4, RetentionPredictionStatus.InsufficientData, null, "C4", "Insufficient"),
            Result(run.Id, 1, RetentionPredictionStatus.Predicted, EmployeeRetentionLabelValue.Short, "C1", "Short"),
            Result(run.Id, 3, RetentionPredictionStatus.Predicted, EmployeeRetentionLabelValue.Long, "C3", "Long")
        ];
        run.FinalCandidateCount = run.Results.Count;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var detail = await CreateService(context).GetRunDetailAsync(job.Id, run.Id);

        Assert.NotNull(detail);
        Assert.Equal(run.Id, detail.RunId);
        Assert.Equal("STORED-CODE", detail.JobRequisitionCodeSnapshot);
        Assert.Equal("Stored Job", detail.JobTitleSnapshot);
        Assert.Equal(10, detail.RetrievalTopN);
        Assert.Equal(5, detail.FinalTopN);
        Assert.Equal(7, detail.CandidatePoolCount);
        Assert.Equal(6, detail.HardFilterPassedCount);
        Assert.Equal(5, detail.RetrievedCandidateCount);
        Assert.Equal(5, detail.FinalCandidateCount);
        Assert.Equal([1, 2, 3, 4, 5], detail.Candidates.Select(candidate => candidate.FinalRank));
        Assert.Equal(
            [EmployeeRetentionLabelValue.Short, EmployeeRetentionLabelValue.Normal,
                EmployeeRetentionLabelValue.Long, null, null],
            detail.Candidates.Select(candidate => candidate.RetentionLabel));
        Assert.Equal(
            [RetentionPredictionStatus.Predicted, RetentionPredictionStatus.Predicted,
                RetentionPredictionStatus.Predicted, RetentionPredictionStatus.InsufficientData,
                RetentionPredictionStatus.Unavailable],
            detail.Candidates.Select(candidate => candidate.RetentionPredictionStatus));
        Assert.Equal("Stored Position", detail.Configuration!.Position.Name);
        Assert.Equal("Stored C#", Assert.Single(detail.Configuration.MandatoryCompetencies).Name);
        Assert.Equal("Stored English", Assert.Single(detail.Configuration.LanguageRequirements).Name);
    }

    [Fact]
    public async Task GetRunDetailAsync_UsesStoredValuesAfterCurrentJobAndCandidateChange()
    {
        await using var context = CreateContext();
        var job = SeedJob(context, "CURRENT-CODE", "Current Job");
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            CandidateCode = "CURRENT-CANDIDATE",
            Person = new Person
            {
                Id = Guid.NewGuid(), AnonymousCode = "ANON", FirstName = "Current", LastName = "Name"
            }
        };
        var run = SeedRun(context, job.Id, DateTime.UtcNow);
        run.Results =
        [
            Result(run.Id, 1, RetentionPredictionStatus.Predicted,
                EmployeeRetentionLabelValue.Long, "STORED-CANDIDATE", "Stored Candidate", candidate.Id)
        ];
        run.FinalCandidateCount = 1;
        context.Candidates.Add(candidate);
        await context.SaveChangesAsync();

        job.RequisitionCode = "CHANGED-JOB";
        job.Title = "Changed Job";
        candidate.CandidateCode = "CHANGED-CANDIDATE";
        candidate.Person.FirstName = "Changed";
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var detail = await CreateService(context).GetRunDetailAsync(job.Id, run.Id);

        Assert.NotNull(detail);
        Assert.Equal("STORED-CODE", detail.JobRequisitionCodeSnapshot);
        Assert.Equal("Stored Job", detail.JobTitleSnapshot);
        Assert.Equal("Stored Job", detail.Configuration!.JobTitle);
        var historicalCandidate = Assert.Single(detail.Candidates);
        Assert.Equal("STORED-CANDIDATE", historicalCandidate.CandidateCodeSnapshot);
        Assert.Equal("Stored Candidate", historicalCandidate.CandidateDisplayNameSnapshot);
        Assert.Equal(1, historicalCandidate.FinalRank);
        Assert.Equal(0.77, historicalCandidate.JobFitScore);
        Assert.Equal(EmployeeRetentionLabelValue.Long, historicalCandidate.RetentionLabel);
    }

    [Fact]
    public async Task GetRunDetailAsync_ZeroResultRunLoadsSuccessfully()
    {
        await using var context = CreateContext();
        var job = SeedJob(context, "JOB", "Job");
        var run = SeedRun(context, job.Id, DateTime.UtcNow);
        run.Results = [];
        run.FinalCandidateCount = 0;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var detail = await CreateService(context).GetRunDetailAsync(job.Id, run.Id);

        Assert.NotNull(detail);
        Assert.Equal(0, detail.FinalCandidateCount);
        Assert.Empty(detail.Candidates);
        Assert.NotNull(detail.Configuration);
    }

    [Fact]
    public async Task GetRunDetailAsync_RejectsWrongJobAndUnknownRun()
    {
        await using var context = CreateContext();
        var jobA = SeedJob(context, "A", "A");
        var jobB = SeedJob(context, "B", "B");
        var run = SeedRun(context, jobB.Id, DateTime.UtcNow);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = CreateService(context);

        Assert.Null(await service.GetRunDetailAsync(jobA.Id, run.Id));
        Assert.Null(await service.GetRunDetailAsync(jobB.Id, Guid.NewGuid()));
    }

    [Theory]
    [InlineData("{ malformed")]
    [InlineData("{\"snapshotSchemaVersion\":\"future-v2\"}")]
    [InlineData("{\"snapshotSchemaVersion\":\"job-matching-configuration-v1\",\"position\":{},\"mandatoryCompetencies\":[null],\"preferredCompetencies\":[],\"languageRequirements\":[]}")]
    public async Task GetRunDetailAsync_MalformedOrUnsupportedConfigurationIsControlled(string json)
    {
        await using var context = CreateContext();
        var job = SeedJob(context, "JOB", "Job");
        var run = SeedRun(context, job.Id, DateTime.UtcNow, json);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var detail = await CreateService(context).GetRunDetailAsync(job.Id, run.Id);

        Assert.NotNull(detail);
        Assert.Equal(json, detail.ConfigurationSnapshotJson);
        Assert.Null(detail.Configuration);
    }

    [Fact]
    public async Task ReadOperations_HaveNoEmbeddingRerankerOrRetentionDependencies()
    {
        var dependencyTypes = typeof(PostgreSqlJobMatchingHistoryService)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Select(parameter => parameter.ParameterType)
            .ToArray();

        Assert.DoesNotContain(typeof(IEmbeddingProvider), dependencyTypes);
        Assert.DoesNotContain(typeof(ICrossEncoderProvider), dependencyTypes);
        Assert.DoesNotContain(typeof(IMlPredictionService), dependencyTypes);

        await using var context = CreateContext();
        var job = SeedJob(context, "JOB", "Job");
        var run = SeedRun(context, job.Id, DateTime.UtcNow);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = CreateService(context);

        Assert.Single(await service.GetRunsForJobAsync(job.Id));
        Assert.NotNull(await service.GetRunDetailAsync(job.Id, run.Id));
    }

    [Fact]
    public async Task MatchingHistoryAction_ReturnsNotFoundForUnknownScopedRun()
    {
        var history = new Mock<IJobMatchingHistoryService>();
        history.Setup(service => service.GetRunDetailAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobMatchingRunDetail?)null);
        var controller = new JobRequisitionController(history.Object);

        var result = await controller.MatchingHistory(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    private static PostgreSqlJobMatchingHistoryService CreateService(HrDecisionSupportDbContext context) =>
        new(
            context,
            Options.Create(new QwenEmbeddingOptions { ModelName = "Qwen/Qwen3-Embedding-0.6B" }),
            Options.Create(new QwenRerankerOptions { ModelName = "Qwen/Qwen3-Reranker-0.6B" }),
            TimeProvider.System);

    private static HrDecisionSupportDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseInMemoryDatabase($"history-read-{Guid.NewGuid():N}")
            .Options);

    private static JobRequisition SeedJob(
        HrDecisionSupportDbContext context,
        string code,
        string title)
    {
        var position = new Position { Id = Guid.NewGuid(), Code = $"P-{code}", Name = $"Position {code}" };
        var job = new JobRequisition
        {
            Id = Guid.NewGuid(), RequisitionCode = code, Title = title,
            PositionId = position.Id, Position = position
        };
        context.AddRange(position, job);
        return job;
    }

    private static JobMatchingRun SeedRun(
        HrDecisionSupportDbContext context,
        Guid jobId,
        DateTime executedAtUtc,
        string configurationJson = ValidConfigurationJson)
    {
        var run = new JobMatchingRun
        {
            Id = Guid.NewGuid(),
            JobRequisitionId = jobId,
            ExecutedAtUtc = executedAtUtc,
            JobRequisitionCodeSnapshot = "STORED-CODE",
            JobTitleSnapshot = "Stored Job",
            RetrievalTopN = 10,
            FinalTopN = 5,
            CandidatePoolCount = 7,
            HardFilterPassedCount = 6,
            RetrievedCandidateCount = 5,
            FinalCandidateCount = 1,
            EmbeddingModelName = "Qwen/Qwen3-Embedding-0.6B",
            RerankerModelName = "Qwen/Qwen3-Reranker-0.6B",
            RetentionModelName = "retention-rf-v1",
            RetentionFeatureSchemaVersion = "retention-features-v1",
            JobDocumentHash = new string('a', 64),
            ConfigurationSnapshotJson = configurationJson,
            Results = []
        };
        context.JobMatchingRuns.Add(run);
        return run;
    }

    private static JobMatchingRunResult Result(
        Guid runId,
        int rank,
        RetentionPredictionStatus status,
        EmployeeRetentionLabelValue? label,
        string code,
        string name,
        Guid? candidateId = null) =>
        new()
        {
            JobMatchingRunId = runId,
            CandidateId = candidateId ?? Guid.NewGuid(),
            CandidateCodeSnapshot = code,
            CandidateDisplayNameSnapshot = name,
            FinalRank = rank,
            SkillTier = rank,
            MandatorySkillCoverage = 0.75m,
            PreferredSkillCoverage = 0.50m,
            EmbeddingScore = 0.81,
            CrossEncoderRawScore = 1.23,
            JobFitScore = 0.77,
            RetentionPredictionStatus = status,
            RetentionLabel = label,
            ShortestPreviousJobMonthsSnapshot = 6,
            LongestPreviousJobMonthsSnapshot = 24
        };
}
