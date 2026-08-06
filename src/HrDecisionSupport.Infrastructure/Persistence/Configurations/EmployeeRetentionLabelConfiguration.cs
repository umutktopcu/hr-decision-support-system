using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class EmployeeRetentionLabelConfiguration
    : IEntityTypeConfiguration<EmployeeRetentionLabel>
{
    public void Configure(EntityTypeBuilder<EmployeeRetentionLabel> builder)
    {
        builder.ToTable(
            "employee_retention_labels",
            table => table.HasCheckConstraint(
                "ck_employee_retention_labels_label_valid",
                "label IN (0, 1, 2)"));
        builder.HasKey(label => label.Id);

        builder.Property(label => label.Id).IsRequired();
        builder.Property(label => label.EmployeeCareerFeatureSnapshotId).IsRequired();
        builder.Property(label => label.Label).HasConversion<int>().IsRequired();
        builder.Property(label => label.LabelSource).HasConversion<int>().IsRequired();
        builder.Property(label => label.LabelDefinitionVersion).HasMaxLength(50).IsRequired();
        builder.Property(label => label.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(label => label.EmployeeCareerFeatureSnapshotId).IsUnique();
        builder.HasOne(label => label.EmployeeCareerFeatureSnapshot)
            .WithOne(snapshot => snapshot.RetentionLabel)
            .HasForeignKey<EmployeeRetentionLabel>(label => label.EmployeeCareerFeatureSnapshotId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
