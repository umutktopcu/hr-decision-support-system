using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class CandidateWorkModePreferenceConfiguration : IEntityTypeConfiguration<CandidateWorkModePreference>
{
    public void Configure(EntityTypeBuilder<CandidateWorkModePreference> builder)
    {
        builder.ToTable("candidate_work_mode_preferences");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Id).IsRequired();
        builder.Property(p => p.CandidateId).IsRequired();
        builder.Property(p => p.WorkModeId).IsRequired();

        builder.HasIndex(p => new { p.CandidateId, p.WorkModeId }).IsUnique();

        builder.HasOne(p => p.Candidate)
            .WithMany(c => c.WorkModePreferences)
            .HasForeignKey(p => p.CandidateId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasOne(p => p.WorkMode)
            .WithMany(w => w.CandidateWorkModePreferences)
            .HasForeignKey(p => p.WorkModeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
