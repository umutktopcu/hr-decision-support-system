using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.MatchingExecution;
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

public sealed class JobMatchingHistoryComparisonTests
{
    private const string ConfigurationA = """
        {
          "snapshotSchemaVersion": "job-matching-configuration-v1",
          "jobTitle": "Stored Job A",
          "jobDescription": "Description A",
          "position": { "id": "11111111-1111-1111-1111-111111111111", "code": "BACKEND", "name": "Backend" },
          "minimumRelevantExperienceMonths": 18,
          "minimumEducation": "Bachelor",
          "mandatorySkillCoverageThreshold": 0.75,
          "workMode": { "id": "22222222-2222-2222-2222-222222222222", "code": "REMOTE", "name": "Remote" },
          "workModeHardFilterEnabled": true,
          "mandatoryCompetencies": [
            { "competencyId": "33333333-3333-3333-3333-333333333333", "code": "CSHARP", "name": "C#" }
          ],
          "preferredCompetencies": [],
          "languageRequirements": [
            { "languageId": "44444444-4444-4444-4444-444444444444", "code": "EN", "name": "English", "minimumProficiency": "B2", "hardFilterEnabled": true }
          ]
        }
        """;

    private const string ConfigurationB = """
        {
          "snapshotSchemaVersion": "job-matching-configuration-v1",
          "jobTitle": "Stored Job B",
          "jobDescription": "Description B",
          "position": { "id": "11111111-1111-1111-1111-111111111111", "code": "BACKEND", "name": "Backend" },
          "minimumRelevantExperienceMonths": 24,
          "minimumEducation": "Bachelor",
          "mandatorySkillCoverageThreshold": 0.80,
          "workMode": { "id": "22222222-2222-2222-2222-222222222222", "code": "REMOTE", "name": "Remote" },
          "workModeHardFilterEnabled": true,
          "mandatoryCompetencies": [
            { "competencyId": "55555555-5555-5555-5555-555555555555", "code": "POSTGRES", "name": "PostgreSQL" }
          ],
          "preferredCompetencies": [],
          "languageRequirements": [
            { "languageId": "44444444-4444-4444-4444-444444444444", "code": "EN", "name": "English", "minimumProficiency": "B2", "hardFilterEnabled": true }
          ]
        }
        """;

    [Fact]
    public async Task GetComparisonAsync_DerivesMovementMembershipRetentionAndSorting()
    {
        await using var context = CreateContext();
        var job = SeedJob(context, "JOB");
        var runA = SeedRun(context, job.Id, "A", finalTopN: 40);
        var runB = SeedRun(context, job.Id, "B", finalTopN: 20);
        var upId = Guid.NewGuid();
        var downId = Guid.NewGuid();
        var unchangedId = Guid.NewGuid();
        var newId = Guid.NewGuid();
        var droppedId = Guid.NewGuid();
        runA.Results =
        [
            Result(runA.Id, upId, 8, "UP-A", "Old Up", RetentionPredictionStatus.Predicted, EmployeeRetentionLabelValue.Normal),
            Result(runA.Id, downId, 3, "DOWN-A", "Down", RetentionPredictionStatus.Unavailable, null),
            Result(runA.Id, unchangedId, 4, "SAME-A", "Same", RetentionPredictionStatus.InsufficientData, null),
            Result(runA.Id, droppedId, 5, "DROP-A", "Dropped", RetentionPredictionStatus.Predicted, EmployeeRetentionLabelValue.Short)
        ];
        runB.Results =
        [
            Result(runB.Id, upId, 3, "UP-B", "New Up", RetentionPredictionStatus.Predicted, EmployeeRetentionLabelValue.Long),
            Result(runB.Id, downId, 8, "DOWN-B", "Down", RetentionPredictionStatus.Unavailable, null),
            Result(runB.Id, unchangedId, 4, "SAME-B", "Same", RetentionPredictionStatus.InsufficientData, null),
            Result(runB.Id, newId, 2, "NEW-B", "New", RetentionPredictionStatus.Predicted, EmployeeRetentionLabelValue.Normal)
        ];
        runA.FinalCandidateCount = runA.Results.Count;
        runB.FinalCandidateCount = runB.Results.Count;
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var comparison = await CreateService(context).GetComparisonAsync(job.Id, runA.Id, runB.Id);

        Assert.NotNull(comparison);
        Assert.Equal(40, comparison.RunA.FinalTopN);
        Assert.Equal(20, comparison.RunB.FinalTopN);
        Assert.Equal([newId, upId, unchangedId, downId, droppedId],
            comparison.Candidates.Select(candidate => candidate.CandidateId));

        var up = comparison.Candidates.Single(candidate => candidate.CandidateId == upId);
        Assert.Equal(5, up.Movement);
        Assert.Equal(CandidateMovementStatus.Up, up.MovementStatus);
        Assert.Equal("UP-B", up.CandidateCodeSnapshot);
        Assert.Equal("New Up", up.CandidateDisplayNameSnapshot);
        Assert.Equal(EmployeeRetentionLabelValue.Normal, up.RetentionLabelA);
        Assert.Equal(EmployeeRetentionLabelValue.Long, up.RetentionLabelB);
        Assert.Equal(1, up.SkillTierA);
        Assert.Equal(2, up.SkillTierB);
        Assert.Equal(0.75m, up.MandatoryCoverageA);
        Assert.Equal(0.80m, up.MandatoryCoverageB);
        Assert.Equal(0.61, up.JobFitScoreA);
        Assert.Equal(0.71, up.JobFitScoreB);

        var down = comparison.Candidates.Single(candidate => candidate.CandidateId == downId);
        Assert.Equal(-5, down.Movement);
        Assert.Equal(CandidateMovementStatus.Down, down.MovementStatus);
        Assert.Equal(RetentionPredictionStatus.Unavailable, down.RetentionStatusA);
        Assert.Equal(RetentionPredictionStatus.Unavailable, down.RetentionStatusB);

        var unchanged = comparison.Candidates.Single(candidate => candidate.CandidateId == unchangedId);
        Assert.Equal(0, unchanged.Movement);
        Assert.Equal(CandidateMovementStatus.Unchanged, unchanged.MovementStatus);

        var added = comparison.Candidates.Single(candidate => candidate.CandidateId == newId);
        Assert.Null(added.RankA);
        Assert.Equal(CandidateMovementStatus.New, added.MovementStatus);

        var dropped = comparison.Candidates.Single(candidate => candidate.CandidateId == droppedId);
        Assert.Null(dropped.RankB);
        Assert.Equal(CandidateMovementStatus.Dropped, dropped.MovementStatus);

        Assert.Equal(new JobMatchingComparisonSummary(3, 1, 1, 1, 1, 1), comparison.Summary);
    }

    [Fact]
    public async Task GetComparisonAsync_ReportsModelDocumentAndTypedConfigurationDifferences()
    {
        await using var context = CreateContext();
        var job = SeedJob(context, "JOB");
        var runA = SeedRun(context, job.Id, "A", configuration: ConfigurationA);
        var runB = SeedRun(context, job.Id, "B", configuration: ConfigurationB);
        runB.EmbeddingModelName = "embedding-v2";
        runB.JobDocumentHash = new string('b', 64);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var comparison = await CreateService(context).GetComparisonAsync(job.Id, runA.Id, runB.Id);

        Assert.NotNull(comparison);
        Assert.True(comparison.ModelConfigurationChanged);
        Assert.True(comparison.SemanticJobDocumentChanged);
        Assert.True(comparison.ConfigurationComparison.IsAvailable);
        Assert.False(comparison.ConfigurationComparison.IsIdentical);
        Assert.Contains(comparison.ConfigurationComparison.Differences,
            difference => difference.Field == "Minimum experience"
                && difference.ValueA == "18" && difference.ValueB == "24");
        Assert.Contains(comparison.ConfigurationComparison.Differences,
            difference => difference.Field == "Mandatory competencies"
                && difference.ValueA == "C#" && difference.ValueB == "PostgreSQL");
    }

    [Fact]
    public async Task GetComparisonAsync_InvalidConfigurationKeepsComparisonAvailable()
    {
        await using var context = CreateContext();
        var job = SeedJob(context, "JOB");
        var runA = SeedRun(context, job.Id, "A", configuration: "{ malformed");
        var runB = SeedRun(context, job.Id, "B", configuration: ConfigurationA);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var comparison = await CreateService(context).GetComparisonAsync(job.Id, runA.Id, runB.Id);

        Assert.NotNull(comparison);
        Assert.False(comparison.ConfigurationComparison.IsAvailable);
        Assert.Null(comparison.ConfigurationComparison.IsIdentical);
        Assert.Empty(comparison.ConfigurationComparison.Differences);
    }

    [Fact]
    public async Task GetComparisonAsync_RejectsSameRunCrossJobAndUnknownRuns()
    {
        await using var context = CreateContext();
        var jobA = SeedJob(context, "A");
        var jobB = SeedJob(context, "B");
        var runA = SeedRun(context, jobA.Id, "A");
        var runB = SeedRun(context, jobB.Id, "B");
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var service = CreateService(context);

        Assert.Null(await service.GetComparisonAsync(jobA.Id, runA.Id, runA.Id));
        Assert.Null(await service.GetComparisonAsync(jobA.Id, runA.Id, runB.Id));
        Assert.Null(await service.GetComparisonAsync(jobA.Id, runA.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task GetComparisonAsync_UsesSnapshotsWithoutCurrentCandidateOrModelDependencies()
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
        Assert.DoesNotContain(typeof(IJobMatchingExecutionService), dependencyTypes);

        await using var context = CreateContext();
        var job = SeedJob(context, "JOB");
        var candidateId = Guid.NewGuid();
        var candidate = new Candidate
        {
            Id = candidateId,
            CandidateCode = "CURRENT",
            Person = new Person
            {
                Id = Guid.NewGuid(), AnonymousCode = "ANON", FirstName = "Current", LastName = "Name"
            }
        };
        var runA = SeedRun(context, job.Id, "A");
        var runB = SeedRun(context, job.Id, "B");
        runA.Results = [Result(runA.Id, candidateId, 1, "STORED-A", "Stored A")];
        runB.Results = [Result(runB.Id, candidateId, 1, "STORED-B", "Stored B")];
        runA.FinalCandidateCount = 1;
        runB.FinalCandidateCount = 1;
        context.Candidates.Add(candidate);
        await context.SaveChangesAsync();

        candidate.CandidateCode = "CHANGED";
        candidate.Person.FirstName = "Changed";
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var comparison = await CreateService(context).GetComparisonAsync(job.Id, runA.Id, runB.Id);

        Assert.NotNull(comparison);
        var result = Assert.Single(comparison.Candidates);
        Assert.Equal("STORED-B", result.CandidateCodeSnapshot);
        Assert.Equal("Stored B", result.CandidateDisplayNameSnapshot);
        Assert.Empty(context.ChangeTracker.Entries<Candidate>());
        Assert.Empty(context.ChangeTracker.Entries<Person>());
    }

    [Fact]
    public async Task CompareAction_ValidatesDistinctAndScopedRuns()
    {
        var history = new Mock<IJobMatchingHistoryService>(MockBehavior.Strict);
        var controller = new JobRequisitionController(history.Object);
        var sameRunId = Guid.NewGuid();

        var sameResult = await controller.CompareMatchingHistory(
            Guid.NewGuid(), sameRunId, sameRunId, CancellationToken.None);

        Assert.IsType<BadRequestResult>(sameResult);
        history.VerifyNoOtherCalls();

        history.Setup(service => service.GetComparisonAsync(
                It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((JobMatchingRunComparison?)null);

        var scopedResult = await controller.CompareMatchingHistory(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(scopedResult);
    }

    private static PostgreSqlJobMatchingHistoryService CreateService(HrDecisionSupportDbContext context) =>
        new(
            context,
            Options.Create(new QwenEmbeddingOptions { ModelName = "embedding-v1" }),
            Options.Create(new QwenRerankerOptions { ModelName = "reranker-v1" }),
            TimeProvider.System);

    private static HrDecisionSupportDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseInMemoryDatabase($"history-comparison-{Guid.NewGuid():N}")
            .Options);

    private static JobRequisition SeedJob(HrDecisionSupportDbContext context, string code)
    {
        var position = new Position { Id = Guid.NewGuid(), Code = $"P-{code}", Name = $"Position {code}" };
        var job = new JobRequisition
        {
            Id = Guid.NewGuid(), RequisitionCode = code, Title = $"Job {code}",
            PositionId = position.Id, Position = position
        };
        context.AddRange(position, job);
        return job;
    }

    private static JobMatchingRun SeedRun(
        HrDecisionSupportDbContext context,
        Guid jobId,
        string marker,
        int finalTopN = 10,
        string configuration = ConfigurationA)
    {
        var run = new JobMatchingRun
        {
            Id = Guid.NewGuid(),
            JobRequisitionId = jobId,
            ExecutedAtUtc = marker == "A"
                ? new DateTime(2026, 8, 17, 10, 0, 0, DateTimeKind.Utc)
                : new DateTime(2026, 8, 18, 10, 0, 0, DateTimeKind.Utc),
            JobRequisitionCodeSnapshot = $"CODE-{marker}",
            JobTitleSnapshot = $"Title {marker}",
            RetrievalTopN = 50,
            FinalTopN = finalTopN,
            CandidatePoolCount = 100,
            HardFilterPassedCount = 80,
            RetrievedCandidateCount = 50,
            FinalCandidateCount = 0,
            EmbeddingModelName = "embedding-v1",
            RerankerModelName = "reranker-v1",
            RetentionModelName = "retention-rf-v1",
            RetentionFeatureSchemaVersion = "retention-features-v1",
            JobDocumentHash = new string('a', 64),
            ConfigurationSnapshotJson = configuration,
            Results = []
        };
        context.JobMatchingRuns.Add(run);
        return run;
    }

    private static JobMatchingRunResult Result(
        Guid runId,
        Guid candidateId,
        int rank,
        string code,
        string name,
        RetentionPredictionStatus status = RetentionPredictionStatus.Predicted,
        EmployeeRetentionLabelValue? label = EmployeeRetentionLabelValue.Normal) =>
        new()
        {
            JobMatchingRunId = runId,
            CandidateId = candidateId,
            CandidateCodeSnapshot = code,
            CandidateDisplayNameSnapshot = name,
            FinalRank = rank,
            SkillTier = code.EndsWith("B", StringComparison.Ordinal) ? 2 : 1,
            MandatorySkillCoverage = code.EndsWith("B", StringComparison.Ordinal) ? 0.80m : 0.75m,
            PreferredSkillCoverage = code.EndsWith("B", StringComparison.Ordinal) ? 0.60m : 0.50m,
            EmbeddingScore = code.EndsWith("B", StringComparison.Ordinal) ? 0.72 : 0.62,
            CrossEncoderRawScore = code.EndsWith("B", StringComparison.Ordinal) ? 2.20 : 1.20,
            JobFitScore = code.EndsWith("B", StringComparison.Ordinal) ? 0.71 : 0.61,
            RetentionPredictionStatus = status,
            RetentionLabel = label,
            ShortestPreviousJobMonthsSnapshot = 6,
            LongestPreviousJobMonthsSnapshot = 24
        };
}
