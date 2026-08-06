using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class EmployeeImportBatchConfiguration : IEntityTypeConfiguration<EmployeeImportBatch>
{
    public void Configure(EntityTypeBuilder<EmployeeImportBatch> builder)
    {
        builder.ToTable("employee_import_batches", table =>
        {
            table.HasCheckConstraint(
                "ck_employee_import_batches_row_counts_non_negative",
                "total_row_count >= 0 AND successful_row_count >= 0 AND failed_row_count >= 0");
            table.HasCheckConstraint(
                "ck_emp_import_batch_completed_counts_lte_total",
                "successful_row_count + failed_row_count <= total_row_count");
            table.HasCheckConstraint(
                "ck_employee_import_batches_file_hash_lowercase_sha256",
                "file_hash ~ '^[0-9a-f]{64}$'");
        });
        builder.HasKey(batch => batch.Id);

        builder.Property(batch => batch.Id).IsRequired();
        builder.Property(batch => batch.FileName).HasMaxLength(260).IsRequired();
        builder.Property(batch => batch.FileHash).HasMaxLength(64).IsRequired();
        builder.Property(batch => batch.DatasetSplit).HasConversion<int>().IsRequired();
        builder.Property(batch => batch.ObservationDate).IsRequired(false);
        builder.Property(batch => batch.ImportedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(batch => batch.TotalRowCount).IsRequired();
        builder.Property(batch => batch.SuccessfulRowCount).IsRequired();
        builder.Property(batch => batch.FailedRowCount).IsRequired();
        builder.Property(batch => batch.Status).HasConversion<int>().IsRequired();

        builder.HasIndex(batch => batch.FileHash).IsUnique();
        builder.UseSnakeCaseColumns();
    }
}
