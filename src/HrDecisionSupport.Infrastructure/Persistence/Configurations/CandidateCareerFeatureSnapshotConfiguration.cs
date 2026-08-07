using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class CandidateCareerFeatureSnapshotConfiguration : IEntityTypeConfiguration<CandidateCareerFeatureSnapshot>
{
    public void Configure(EntityTypeBuilder<CandidateCareerFeatureSnapshot> builder)
    {
        builder.ToTable("candidate_career_feature_snapshots", table =>
        {
            table.HasCheckConstraint(
                "ck_candidate_career_feature_snapshots_non_negative",
                "(total_experience_months IS NULL OR total_experience_months >= 0) "
                + "AND (backend_experience_months IS NULL OR backend_experience_months >= 0) "
                + "AND (previous_company_average_stay_months IS NULL OR previous_company_average_stay_months >= 0) "
                + "AND (shortest_previous_job_months IS NULL OR shortest_previous_job_months >= 0) "
                + "AND (longest_previous_job_months IS NULL OR longest_previous_job_months >= 0) "
                + "AND (last_previous_company_stay_months IS NULL OR last_previous_company_stay_months >= 0) "
                + "AND (company_change_count IS NULL OR company_change_count >= 0) "
                + "AND (job_change_rate IS NULL OR job_change_rate >= 0)");
                
            table.HasCheckConstraint(
                "ck_candidate_feature_backend_lte_total",
                "backend_experience_months IS NULL OR total_experience_months IS NULL OR backend_experience_months <= total_experience_months");
                
            table.HasCheckConstraint(
                "ck_candidate_feature_shortest_lte_longest",
                "shortest_previous_job_months IS NULL OR longest_previous_job_months IS NULL OR shortest_previous_job_months <= longest_previous_job_months");
        });
        
        builder.HasKey(snapshot => snapshot.Id);

        builder.Property(snapshot => snapshot.Id).IsRequired();
        builder.Property(snapshot => snapshot.CandidateId).IsRequired();
        
        builder.Property(snapshot => snapshot.TotalExperienceMonths).IsRequired(false);
        builder.Property(snapshot => snapshot.BackendExperienceMonths).IsRequired(false);
        
        builder.Property(snapshot => snapshot.PreviousCompanyAverageStayMonths)
            .HasPrecision(6, 1)
            .IsRequired(false);
            
        builder.Property(snapshot => snapshot.ShortestPreviousJobMonths).IsRequired(false);
        builder.Property(snapshot => snapshot.LongestPreviousJobMonths).IsRequired(false);
        builder.Property(snapshot => snapshot.LastPreviousCompanyStayMonths).IsRequired(false);
        builder.Property(snapshot => snapshot.CompanyChangeCount).IsRequired(false);
        
        builder.Property(snapshot => snapshot.JobChangeRate)
            .HasPrecision(12, 6)
            .IsRequired(false);
            
        builder.Property(snapshot => snapshot.FeatureSchemaVersion).HasMaxLength(50).IsRequired();
        builder.Property(snapshot => snapshot.CalculatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(snapshot => new { snapshot.CandidateId, snapshot.FeatureSchemaVersion }).IsUnique();

        builder.HasOne(snapshot => snapshot.Candidate)
            .WithMany(candidate => candidate.CareerFeatureSnapshots)
            .HasForeignKey(snapshot => snapshot.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
