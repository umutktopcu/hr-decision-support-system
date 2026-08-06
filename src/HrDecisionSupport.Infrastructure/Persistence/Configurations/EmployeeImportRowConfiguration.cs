using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class EmployeeImportRowConfiguration : IEntityTypeConfiguration<EmployeeImportRow>
{
    public void Configure(EntityTypeBuilder<EmployeeImportRow> builder)
    {
        builder.ToTable(
            "employee_import_rows",
            table => table.HasCheckConstraint(
                "ck_employee_import_rows_source_row_number_positive",
                "source_row_number > 0"));
        builder.HasKey(row => row.Id);

        builder.Property(row => row.Id).IsRequired();
        builder.Property(row => row.ImportBatchId).IsRequired();
        builder.Property(row => row.SourceRowNumber).IsRequired();
        builder.Property(row => row.ExternalEmployeeCode).HasMaxLength(50).IsRequired(false);
        builder.Property(row => row.RawPayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(row => row.ImportStatus).HasConversion<int>().IsRequired();
        builder.Property(row => row.ValidationErrorsJson).HasColumnType("jsonb").IsRequired(false);
        builder.Property(row => row.EmployeeId).IsRequired(false);
        builder.Property(row => row.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(row => new { row.ImportBatchId, row.SourceRowNumber }).IsUnique();
        builder.HasIndex(row => new { row.ImportBatchId, row.ExternalEmployeeCode })
            .IsUnique()
            .HasFilter("\"external_employee_code\" IS NOT NULL");
        builder.HasIndex(row => row.EmployeeId);

        builder.HasOne(row => row.ImportBatch)
            .WithMany(batch => batch.Rows)
            .HasForeignKey(row => row.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(row => row.Employee)
            .WithMany(employee => employee.ImportRows)
            .HasForeignKey(row => row.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
