using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.ToTable("candidates");
        builder.HasKey(candidate => candidate.Id);

        builder.Property(candidate => candidate.Id).IsRequired();
        builder.Property(candidate => candidate.PersonId).IsRequired();
        builder.Property(candidate => candidate.CandidateCode).HasMaxLength(50).IsRequired();
        builder.Property(candidate => candidate.CandidateSource).HasConversion<int>().IsRequired();
        builder.Property(candidate => candidate.ExternalCandidateId).HasMaxLength(200).IsRequired(false);
        builder.Property(candidate => candidate.ProfessionalTitle).HasMaxLength(200).IsRequired(false);
        builder.Property(candidate => candidate.AvailabilityDays).IsRequired(false);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint(
                "ck_candidates_availability_days_non_negative",
                "availability_days IS NULL OR availability_days >= 0");
        });

        builder.HasIndex(candidate => candidate.PersonId).IsUnique();
        builder.HasIndex(candidate => candidate.CandidateCode).IsUnique();

        builder.HasOne(candidate => candidate.Person)
            .WithOne(person => person.Candidate)
            .HasForeignKey<Candidate>(candidate => candidate.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
