using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Pgvector.EntityFrameworkCore;

namespace HrDecisionSupport.Tests.Persistence;

public sealed class JobMatchingHistoryPersistenceModelTests
{
    [Fact]
    public void JobMatchingRun_MapsRequiredSnapshotAndModelMetadata()
    {
        var entity = GetEntity<JobMatchingRun>();

        Assert.Equal("job_matching_runs", entity.GetTableName());
        Assert.Equal(nameof(JobMatchingRun.Id), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        AssertRequired(entity, nameof(JobMatchingRun.Id));
        AssertRequired(entity, nameof(JobMatchingRun.JobRequisitionId));
        AssertRequired(entity, nameof(JobMatchingRun.ExecutedAtUtc), "timestamp with time zone");
        AssertRequired(entity, nameof(JobMatchingRun.JobRequisitionCodeSnapshot), maxLength: 50);
        AssertRequired(entity, nameof(JobMatchingRun.JobTitleSnapshot), maxLength: 250);
        AssertRequired(entity, nameof(JobMatchingRun.RetrievalTopN));
        AssertRequired(entity, nameof(JobMatchingRun.FinalTopN));
        AssertRequired(entity, nameof(JobMatchingRun.CandidatePoolCount));
        AssertRequired(entity, nameof(JobMatchingRun.HardFilterPassedCount));
        AssertRequired(entity, nameof(JobMatchingRun.RetrievedCandidateCount));
        AssertRequired(entity, nameof(JobMatchingRun.FinalCandidateCount));
        AssertRequired(entity, nameof(JobMatchingRun.EmbeddingModelName), maxLength: 200);
        AssertRequired(entity, nameof(JobMatchingRun.RerankerModelName), maxLength: 200);
        AssertRequired(entity, nameof(JobMatchingRun.RetentionModelName), maxLength: 200);
        AssertRequired(entity, nameof(JobMatchingRun.RetentionFeatureSchemaVersion), maxLength: 50);
        AssertRequired(entity, nameof(JobMatchingRun.JobDocumentHash), maxLength: 64);
        AssertRequired(entity, nameof(JobMatchingRun.ConfigurationSnapshotJson), "jsonb");
    }

    [Fact]
    public void JobMatchingRun_HasHistoryListingIndexAndRestrictiveJobRelationship()
    {
        var entity = GetEntity<JobMatchingRun>();
        var index = Assert.Single(entity.GetIndexes());

        Assert.False(index.IsUnique);
        Assert.Equal(
            [nameof(JobMatchingRun.JobRequisitionId), nameof(JobMatchingRun.ExecutedAtUtc)],
            index.Properties.Select(property => property.Name));

        var jobForeignKey = Assert.Single(
            entity.GetForeignKeys(),
            foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(JobRequisition));
        Assert.True(jobForeignKey.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, jobForeignKey.DeleteBehavior);
        Assert.Equal(nameof(JobMatchingRun.JobRequisitionId), Assert.Single(jobForeignKey.Properties).Name);
    }

    [Fact]
    public void JobMatchingRunResult_HasCompositeKeyAndUniqueRunRank()
    {
        var entity = GetEntity<JobMatchingRunResult>();

        Assert.Equal("job_matching_run_results", entity.GetTableName());
        Assert.Equal(
            [nameof(JobMatchingRunResult.JobMatchingRunId), nameof(JobMatchingRunResult.CandidateId)],
            entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
        var rankIndex = Assert.Single(entity.GetIndexes());
        Assert.True(rankIndex.IsUnique);
        Assert.Equal(
            [nameof(JobMatchingRunResult.JobMatchingRunId), nameof(JobMatchingRunResult.FinalRank)],
            rankIndex.Properties.Select(property => property.Name));
    }

    [Fact]
    public void JobMatchingRunResult_MapsScoresRetentionAndNullableSnapshots()
    {
        var entity = GetEntity<JobMatchingRunResult>();

        AssertRequired(entity, nameof(JobMatchingRunResult.CandidateId));
        AssertOptional(entity, nameof(JobMatchingRunResult.CandidateCodeSnapshot), maxLength: 50);
        AssertRequired(entity, nameof(JobMatchingRunResult.CandidateDisplayNameSnapshot), maxLength: 250);
        AssertRequired(entity, nameof(JobMatchingRunResult.FinalRank));
        AssertRequired(entity, nameof(JobMatchingRunResult.SkillTier));
        AssertRequired(entity, nameof(JobMatchingRunResult.MandatorySkillCoverage), "numeric");
        AssertRequired(entity, nameof(JobMatchingRunResult.PreferredSkillCoverage), "numeric");
        AssertRequired(entity, nameof(JobMatchingRunResult.EmbeddingScore), "double precision");
        AssertRequired(entity, nameof(JobMatchingRunResult.CrossEncoderRawScore), "double precision");
        AssertRequired(entity, nameof(JobMatchingRunResult.JobFitScore), "double precision");
        AssertRequired(entity, nameof(JobMatchingRunResult.RetentionPredictionStatus));
        Assert.Equal(typeof(RetentionPredictionStatus), entity.FindProperty(nameof(JobMatchingRunResult.RetentionPredictionStatus))!.ClrType);
        AssertOptional(entity, nameof(JobMatchingRunResult.RetentionLabel));
        Assert.Equal(typeof(EmployeeRetentionLabelValue?), entity.FindProperty(nameof(JobMatchingRunResult.RetentionLabel))!.ClrType);
        AssertOptional(entity, nameof(JobMatchingRunResult.ShortestPreviousJobMonthsSnapshot));
        AssertOptional(entity, nameof(JobMatchingRunResult.LongestPreviousJobMonthsSnapshot));
    }

    [Fact]
    public void JobMatchingRunResult_CascadesWithRunAndHasNoCandidateForeignKey()
    {
        var entity = GetEntity<JobMatchingRunResult>();
        var foreignKey = Assert.Single(entity.GetForeignKeys());

        Assert.Equal(typeof(JobMatchingRun), foreignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.DoesNotContain(
            entity.GetForeignKeys(),
            candidate => candidate.PrincipalEntityType.ClrType == typeof(Candidate));
    }

    [Fact]
    public void DbContexts_ExposeHistorySetsAsReadOnlyDbSets()
    {
        AssertDbSet<JobMatchingRun>(nameof(IHrDecisionSupportDbContext.JobMatchingRuns));
        AssertDbSet<JobMatchingRunResult>(nameof(IHrDecisionSupportDbContext.JobMatchingRunResults));
    }

    private static IEntityType GetEntity<TEntity>() where TEntity : class
    {
        var options = new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseNpgsql(npgsqlOptions => npgsqlOptions.UseVector())
            .Options;
        using var context = new HrDecisionSupportDbContext(options);
        return context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is missing from the model.");
    }

    private static void AssertDbSet<TEntity>(string propertyName) where TEntity : class
    {
        var property = typeof(IHrDecisionSupportDbContext).GetProperty(propertyName);

        Assert.NotNull(property);
        Assert.Equal(typeof(DbSet<TEntity>), property.PropertyType);
        Assert.False(property.CanWrite);
    }

    private static void AssertRequired(
        IEntityType entity,
        string propertyName,
        string? columnType = null,
        int? maxLength = null)
    {
        var property = entity.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.False(property.IsNullable);
        if (columnType is not null) Assert.Equal(columnType, property.GetColumnType());
        if (maxLength is not null) Assert.Equal(maxLength, property.GetMaxLength());
    }

    private static void AssertOptional(IEntityType entity, string propertyName, int? maxLength = null)
    {
        var property = entity.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.True(property.IsNullable);
        if (maxLength is not null) Assert.Equal(maxLength, property.GetMaxLength());
    }
}
