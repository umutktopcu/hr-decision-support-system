using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public sealed class JobMatchingRunConfiguration : IEntityTypeConfiguration<JobMatchingRun>
{
    public void Configure(EntityTypeBuilder<JobMatchingRun> builder)
    {
        builder.ToTable("job_matching_runs", table =>
        {
            table.HasCheckConstraint(
                "ck_job_matching_runs_top_n_positive",
                "retrieval_top_n > 0 AND final_top_n > 0");
            table.HasCheckConstraint(
                "ck_job_matching_runs_counts_non_negative",
                "candidate_pool_count >= 0 "
                + "AND hard_filter_passed_count >= 0 "
                + "AND retrieved_candidate_count >= 0 "
                + "AND final_candidate_count >= 0");
        });
        builder.HasKey(run => run.Id);

        builder.Property(run => run.Id).IsRequired();
        builder.Property(run => run.JobRequisitionId).IsRequired();
        builder.Property(run => run.ExecutedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(run => run.JobRequisitionCodeSnapshot).HasMaxLength(50).IsRequired();
        builder.Property(run => run.JobTitleSnapshot).HasMaxLength(250).IsRequired();
        builder.Property(run => run.RetrievalTopN).IsRequired();
        builder.Property(run => run.FinalTopN).IsRequired();
        builder.Property(run => run.CandidatePoolCount).IsRequired();
        builder.Property(run => run.HardFilterPassedCount).IsRequired();
        builder.Property(run => run.RetrievedCandidateCount).IsRequired();
        builder.Property(run => run.FinalCandidateCount).IsRequired();
        builder.Property(run => run.EmbeddingModelName).HasMaxLength(200).IsRequired();
        builder.Property(run => run.RerankerModelName).HasMaxLength(200).IsRequired();
        builder.Property(run => run.RetentionModelName).HasMaxLength(200).IsRequired();
        builder.Property(run => run.RetentionFeatureSchemaVersion).HasMaxLength(50).IsRequired();
        builder.Property(run => run.JobDocumentHash).HasMaxLength(64).IsRequired();
        builder.Property(run => run.ConfigurationSnapshotJson)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.HasIndex(run => new { run.JobRequisitionId, run.ExecutedAtUtc });

        builder.HasOne(run => run.JobRequisition)
            .WithMany()
            .HasForeignKey(run => run.JobRequisitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(run => run.Results)
            .WithOne(result => result.JobMatchingRun)
            .HasForeignKey(result => result.JobMatchingRunId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.UseSnakeCaseColumns();
    }
}
