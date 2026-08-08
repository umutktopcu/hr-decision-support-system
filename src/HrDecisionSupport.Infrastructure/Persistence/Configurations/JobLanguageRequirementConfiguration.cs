using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class JobLanguageRequirementConfiguration : IEntityTypeConfiguration<JobLanguageRequirement>
{
    public void Configure(EntityTypeBuilder<JobLanguageRequirement> builder)
    {
        builder.ToTable("job_language_requirements");

        builder.HasKey(req => req.Id);

        builder.Property(req => req.Id).IsRequired();
        builder.Property(req => req.JobRequisitionId).IsRequired();
        builder.Property(req => req.LanguageId).IsRequired();
        builder.Property(req => req.MinimumProficiency)
            .HasConversion<int>()
            .IsRequired();
        builder.Property(req => req.HardFilterEnabled).IsRequired();

        // Foreign keys
        builder.HasOne(req => req.JobRequisition)
            .WithMany(jr => jr.LanguageRequirements)
            .HasForeignKey(req => req.JobRequisitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(req => req.Language)
            .WithMany()
            .HasForeignKey(req => req.LanguageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.UseSnakeCaseColumns();
    }
}
