using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public sealed class JobMatchingRunResultConfiguration
    : IEntityTypeConfiguration<JobMatchingRunResult>
{
    public void Configure(EntityTypeBuilder<JobMatchingRunResult> builder)
    {
        builder.ToTable("job_matching_run_results", table =>
        {
            table.HasCheckConstraint(
                "ck_job_matching_run_results_rank_tier_positive",
                "final_rank > 0 AND skill_tier > 0");
            table.HasCheckConstraint(
                "ck_job_matching_run_results_coverage_range",
                "mandatory_skill_coverage >= 0 AND mandatory_skill_coverage <= 1 "
                + "AND preferred_skill_coverage >= 0 AND preferred_skill_coverage <= 1");
            table.HasCheckConstraint(
                "ck_job_matching_run_results_retention_status_valid",
                "retention_prediction_status IN (1, 2, 3)");
            table.HasCheckConstraint(
                "ck_job_matching_run_results_retention_label_valid",
                "retention_label IS NULL OR retention_label IN (0, 1, 2)");
            table.HasCheckConstraint(
                "ck_job_matching_run_results_months_non_negative",
                "(shortest_previous_job_months_snapshot IS NULL "
                + "OR shortest_previous_job_months_snapshot >= 0) "
                + "AND (longest_previous_job_months_snapshot IS NULL "
                + "OR longest_previous_job_months_snapshot >= 0)");
            table.HasCheckConstraint(
                "ck_job_matching_run_results_shortest_lte_longest",
                "shortest_previous_job_months_snapshot IS NULL "
                + "OR longest_previous_job_months_snapshot IS NULL "
                + "OR shortest_previous_job_months_snapshot <= longest_previous_job_months_snapshot");
        });
        builder.HasKey(result => new { result.JobMatchingRunId, result.CandidateId });

        builder.Property(result => result.JobMatchingRunId).IsRequired();
        builder.Property(result => result.CandidateId).IsRequired();
        builder.Property(result => result.CandidateCodeSnapshot).HasMaxLength(50).IsRequired(false);
        builder.Property(result => result.CandidateDisplayNameSnapshot).HasMaxLength(250).IsRequired();
        builder.Property(result => result.FinalRank).IsRequired();
        builder.Property(result => result.SkillTier).IsRequired();
        builder.Property(result => result.MandatorySkillCoverage)
            .HasColumnType("numeric")
            .IsRequired();
        builder.Property(result => result.PreferredSkillCoverage)
            .HasColumnType("numeric")
            .IsRequired();
        builder.Property(result => result.EmbeddingScore)
            .HasColumnType("double precision")
            .IsRequired();
        builder.Property(result => result.CrossEncoderRawScore)
            .HasColumnType("double precision")
            .IsRequired();
        builder.Property(result => result.JobFitScore)
            .HasColumnType("double precision")
            .IsRequired();
        builder.Property(result => result.RetentionPredictionStatus)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(result => result.RetentionLabel)
            .HasConversion<int>()
            .IsRequired(false);
        builder.Property(result => result.ShortestPreviousJobMonthsSnapshot).IsRequired(false);
        builder.Property(result => result.LongestPreviousJobMonthsSnapshot).IsRequired(false);

        builder.HasIndex(result => new { result.JobMatchingRunId, result.FinalRank }).IsUnique();

        builder.UseSnakeCaseColumns();
    }
}
