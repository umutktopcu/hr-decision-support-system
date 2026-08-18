using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence.Embeddings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public sealed class JobRequisitionEmbeddingConfiguration : IEntityTypeConfiguration<JobRequisitionEmbedding>
{
    public void Configure(EntityTypeBuilder<JobRequisitionEmbedding> builder)
    {
        builder.ToTable("job_requisition_embeddings");
        builder.HasKey(embedding => embedding.JobRequisitionId);

        builder.Property(embedding => embedding.JobRequisitionId).IsRequired();
        builder.Property(embedding => embedding.DocumentHash).HasMaxLength(64).IsRequired();
        builder.Property(embedding => embedding.ModelName).HasMaxLength(200).IsRequired();
        builder.Property(embedding => embedding.EmbeddingVector)
            .HasColumnType("vector(1024)")
            .IsRequired();
        builder.Property(embedding => embedding.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<JobRequisition>()
            .WithOne()
            .HasForeignKey<JobRequisitionEmbedding>(embedding => embedding.JobRequisitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.UseSnakeCaseColumns();
    }
}
