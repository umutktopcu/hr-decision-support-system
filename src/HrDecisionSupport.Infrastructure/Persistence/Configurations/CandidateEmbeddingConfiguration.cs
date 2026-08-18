using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence.Embeddings;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public sealed class CandidateEmbeddingConfiguration : IEntityTypeConfiguration<CandidateEmbedding>
{
    public void Configure(EntityTypeBuilder<CandidateEmbedding> builder)
    {
        builder.ToTable("candidate_embeddings");
        builder.HasKey(embedding => embedding.CandidateId);

        builder.Property(embedding => embedding.CandidateId).IsRequired();
        builder.Property(embedding => embedding.DocumentHash).HasMaxLength(64).IsRequired();
        builder.Property(embedding => embedding.ModelName).HasMaxLength(200).IsRequired();
        builder.Property(embedding => embedding.EmbeddingVector)
            .HasColumnType("vector(1024)")
            .IsRequired();
        builder.Property(embedding => embedding.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasOne<Candidate>()
            .WithOne()
            .HasForeignKey<CandidateEmbedding>(embedding => embedding.CandidateId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.UseSnakeCaseColumns();
    }
}
