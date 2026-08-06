using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class PersonPriorPositionEvidenceConfiguration
    : IEntityTypeConfiguration<PersonPriorPositionEvidence>
{
    public void Configure(EntityTypeBuilder<PersonPriorPositionEvidence> builder)
    {
        builder.ToTable(
            "person_prior_position_evidences",
            table => table.HasCheckConstraint(
                "ck_person_prior_position_evidences_sequence_number_positive",
                "sequence_number > 0"));
        builder.HasKey(evidence => evidence.Id);

        builder.Property(evidence => evidence.Id).IsRequired();
        builder.Property(evidence => evidence.PersonId).IsRequired();
        builder.Property(evidence => evidence.Title).HasMaxLength(200).IsRequired();
        builder.Property(evidence => evidence.SequenceNumber).IsRequired();
        builder.Property(evidence => evidence.ImportRowId).IsRequired();
        builder.Property(evidence => evidence.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.HasIndex(evidence => new
        {
            evidence.PersonId,
            evidence.SequenceNumber,
            evidence.Title
        }).IsUnique();

        builder.HasOne(evidence => evidence.Person)
            .WithMany(person => person.PriorPositionEvidences)
            .HasForeignKey(evidence => evidence.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(evidence => evidence.ImportRow)
            .WithMany(row => row.PriorPositionEvidences)
            .HasForeignKey(evidence => evidence.ImportRowId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
