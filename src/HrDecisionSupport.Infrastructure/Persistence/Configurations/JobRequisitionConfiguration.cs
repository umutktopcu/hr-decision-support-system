using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class JobRequisitionConfiguration : IEntityTypeConfiguration<JobRequisition>
{
    public void Configure(EntityTypeBuilder<JobRequisition> builder)
    {
        builder.ToTable("job_requisitions", table =>
        {
            table.HasCheckConstraint(
                "ck_job_requisitions_openings_count",
                "openings_count > 0");
            table.HasCheckConstraint(
                "ck_job_requisitions_closed_at_not_before_opened_at",
                "closed_at IS NULL OR closed_at >= opened_at");
        });
        builder.HasKey(requisition => requisition.Id);

        builder.Property(requisition => requisition.Id).IsRequired();
        builder.Property(requisition => requisition.RequisitionCode).HasMaxLength(50).IsRequired();
        builder.Property(requisition => requisition.Title).HasMaxLength(250).IsRequired();
        builder.Property(requisition => requisition.DepartmentId).IsRequired();
        builder.Property(requisition => requisition.PositionId).IsRequired();
        builder.Property(requisition => requisition.Description).HasMaxLength(2000).IsRequired(false);
        builder.Property(requisition => requisition.OpeningsCount).IsRequired();
        builder.Property(requisition => requisition.JobRequisitionStatus)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(requisition => requisition.OpenedAt).IsRequired();
        builder.Property(requisition => requisition.ClosedAt).IsRequired(false);
        builder.Property(requisition => requisition.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(requisition => requisition.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.HasIndex(requisition => requisition.RequisitionCode).IsUnique();
        builder.HasIndex(requisition => requisition.DepartmentId);
        builder.HasIndex(requisition => requisition.PositionId);

        builder.HasOne(requisition => requisition.Department)
            .WithMany(department => department.JobRequisitions)
            .HasForeignKey(requisition => requisition.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requisition => requisition.Position)
            .WithMany(position => position.JobRequisitions)
            .HasForeignKey(requisition => requisition.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
