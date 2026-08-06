using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class EmployeeCareerFeatureSnapshotConfiguration
    : IEntityTypeConfiguration<EmployeeCareerFeatureSnapshot>
{
    public void Configure(EntityTypeBuilder<EmployeeCareerFeatureSnapshot> builder)
    {
        builder.ToTable("employee_career_feature_snapshots", table =>
        {
            table.HasCheckConstraint(
                "ck_employee_career_feature_snapshots_months_non_negative",
                "(total_experience_months IS NULL OR total_experience_months >= 0) "
                + "AND (backend_experience_months IS NULL OR backend_experience_months >= 0) "
                + "AND (previous_company_average_stay_months IS NULL OR previous_company_average_stay_months >= 0) "
                + "AND (shortest_previous_job_months IS NULL OR shortest_previous_job_months >= 0) "
                + "AND (longest_previous_job_months IS NULL OR longest_previous_job_months >= 0) "
                + "AND (last_previous_company_stay_months IS NULL OR last_previous_company_stay_months >= 0) "
                + "AND (company_change_count IS NULL OR company_change_count >= 0) "
                + "AND (observed_company_tenure_months IS NULL OR observed_company_tenure_months >= 0)");
            table.HasCheckConstraint(
                "ck_emp_feature_backend_lte_total",
                "backend_experience_months IS NULL OR total_experience_months IS NULL OR backend_experience_months <= total_experience_months");
            table.HasCheckConstraint(
                "ck_emp_feature_shortest_lte_longest",
                "shortest_previous_job_months IS NULL OR longest_previous_job_months IS NULL OR shortest_previous_job_months <= longest_previous_job_months");
            table.HasCheckConstraint(
                "ck_emp_feature_job_change_rate_non_negative",
                "imported_job_change_rate IS NULL OR imported_job_change_rate >= 0");
        });
        builder.HasKey(snapshot => snapshot.Id);

        builder.Property(snapshot => snapshot.Id).IsRequired();
        builder.Property(snapshot => snapshot.EmployeeId).IsRequired();
        builder.Property(snapshot => snapshot.ImportBatchId).IsRequired();
        builder.Property(snapshot => snapshot.ObservedAt).IsRequired(false);
        builder.Property(snapshot => snapshot.TotalExperienceMonths).IsRequired(false);
        builder.Property(snapshot => snapshot.BackendExperienceMonths).IsRequired(false);
        builder.Property(snapshot => snapshot.HasPreviousCompany).IsRequired(false);
        builder.Property(snapshot => snapshot.PreviousCompanyAverageStayMonths).IsRequired(false);
        builder.Property(snapshot => snapshot.ShortestPreviousJobMonths).IsRequired(false);
        builder.Property(snapshot => snapshot.LongestPreviousJobMonths).IsRequired(false);
        builder.Property(snapshot => snapshot.LastPreviousCompanyStayMonths).IsRequired(false);
        builder.Property(snapshot => snapshot.CompanyChangeCount).IsRequired(false);
        builder.Property(snapshot => snapshot.ImportedJobChangeRate)
            .HasPrecision(12, 6)
            .IsRequired(false);
        builder.Property(snapshot => snapshot.ObservedCompanyTenureMonths).IsRequired(false);
        builder.Property(snapshot => snapshot.FeatureSource).HasConversion<int>().IsRequired();
        builder.Property(snapshot => snapshot.FeatureSchemaVersion).HasMaxLength(50).IsRequired();
        builder.Property(snapshot => snapshot.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(snapshot => new
        {
            snapshot.EmployeeId,
            snapshot.ImportBatchId,
            snapshot.FeatureSchemaVersion
        }).IsUnique();
        builder.HasIndex(snapshot => snapshot.ImportBatchId);

        builder.HasOne(snapshot => snapshot.Employee)
            .WithMany(employee => employee.CareerFeatureSnapshots)
            .HasForeignKey(snapshot => snapshot.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(snapshot => snapshot.ImportBatch)
            .WithMany(batch => batch.FeatureSnapshots)
            .HasForeignKey(snapshot => snapshot.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
