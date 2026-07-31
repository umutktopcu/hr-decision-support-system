using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class JobRequisitionRequirementConfiguration
    : IEntityTypeConfiguration<JobRequisitionRequirement>
{
    public void Configure(EntityTypeBuilder<JobRequisitionRequirement> builder)
    {
        builder.ToTable(
            "job_requisition_requirements",
            table => table.HasCheckConstraint(
                "ck_job_requisition_requirements_min_exp_months_non_negative",
                "minimum_experience_months IS NULL OR minimum_experience_months >= 0"));
        builder.HasKey(requirement => requirement.Id);

        builder.Property(requirement => requirement.Id).IsRequired();
        builder.Property(requirement => requirement.JobRequisitionId).IsRequired();
        builder.Property(requirement => requirement.CompetencyId).IsRequired();
        builder.Property(requirement => requirement.MinimumExperienceMonths).IsRequired(false);
        builder.Property(requirement => requirement.MinimumProficiencyLevel)
            .HasConversion<int>()
            .IsRequired(false);
        builder.Property(requirement => requirement.IsRequired).IsRequired();
        builder.Property(requirement => requirement.Notes).HasMaxLength(1000).IsRequired(false);

        builder.HasIndex(requirement => new
        {
            requirement.JobRequisitionId,
            requirement.CompetencyId
        }).IsUnique();

        builder.HasOne(requirement => requirement.JobRequisition)
            .WithMany(requisition => requisition.Requirements)
            .HasForeignKey(requirement => requirement.JobRequisitionId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(requirement => requirement.Competency)
            .WithMany(competency => competency.JobRequisitionRequirements)
            .HasForeignKey(requirement => requirement.CompetencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
