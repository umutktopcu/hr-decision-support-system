using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;
using HrDecisionSupport.Infrastructure.Persistence.Embeddings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Pgvector.EntityFrameworkCore;

namespace HrDecisionSupport.Tests.Persistence;

public sealed class EmbeddingPersistenceModelTests
{
    [Theory]
    [InlineData(typeof(CandidateEmbedding), "candidate_embeddings", nameof(CandidateEmbedding.CandidateId))]
    [InlineData(typeof(JobRequisitionEmbedding), "job_requisition_embeddings", nameof(JobRequisitionEmbedding.JobRequisitionId))]
    public void EmbeddingRecord_MapsRequiredColumns(
        Type recordType,
        string tableName,
        string ownerIdPropertyName)
    {
        var entity = GetEntity(CreateModel(), recordType);

        Assert.Equal(tableName, entity.GetTableName());
        Assert.Equal(ownerIdPropertyName, Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        AssertRequired(entity, ownerIdPropertyName);
        AssertRequired(entity, "DocumentHash");
        AssertRequired(entity, "ModelName");
        AssertRequired(entity, "EmbeddingVector");
        AssertRequired(entity, "UpdatedAtUtc");
        Assert.Equal("vector(1024)", entity.FindProperty("EmbeddingVector")!.GetColumnType());
        Assert.Equal("timestamp with time zone", entity.FindProperty("UpdatedAtUtc")!.GetColumnType());
        Assert.Empty(entity.GetIndexes());
    }

    [Theory]
    [InlineData(typeof(CandidateEmbedding), typeof(Candidate), nameof(CandidateEmbedding.CandidateId))]
    [InlineData(typeof(JobRequisitionEmbedding), typeof(JobRequisition), nameof(JobRequisitionEmbedding.JobRequisitionId))]
    public void EmbeddingRecord_UsesOwnerPrimaryKeyAsCascadeForeignKey(
        Type recordType,
        Type ownerType,
        string ownerIdPropertyName)
    {
        var entity = GetEntity(CreateModel(), recordType);
        var foreignKey = Assert.Single(
            entity.GetForeignKeys(),
            candidate => candidate.PrincipalEntityType.ClrType == ownerType);

        Assert.True(foreignKey.IsRequired);
        Assert.True(foreignKey.IsUnique);
        Assert.Equal(DeleteBehavior.Cascade, foreignKey.DeleteBehavior);
        Assert.Equal(ownerIdPropertyName, Assert.Single(foreignKey.Properties).Name);
        Assert.Equal(
            ownerIdPropertyName,
            Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
    }

    [Fact]
    public void Model_DeclaresPgvectorExtension()
    {
        var model = CreateModel();

        Assert.NotNull(model.FindAnnotation("Npgsql:PostgresExtension:vector"));
    }

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseNpgsql(npgsqlOptions => npgsqlOptions.UseVector())
            .Options;
        using var context = new HrDecisionSupportDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }

    private static IEntityType GetEntity(IModel model, Type type)
    {
        return model.FindEntityType(type)
            ?? throw new InvalidOperationException($"{type.Name} is missing from the model.");
    }

    private static void AssertRequired(IEntityType entity, string propertyName)
    {
        var property = entity.FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.False(property.IsNullable);
    }
}
