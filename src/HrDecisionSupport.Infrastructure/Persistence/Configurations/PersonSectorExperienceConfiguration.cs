using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class PersonSectorExperienceConfiguration : IEntityTypeConfiguration<PersonSectorExperience>
{
    public void Configure(EntityTypeBuilder<PersonSectorExperience> builder)
    {
        builder.ToTable(
            "person_sector_experiences",
            table => table.HasCheckConstraint(
                "ck_person_sector_experiences_experience_months_non_negative",
                "experience_months IS NULL OR experience_months >= 0"));
        builder.HasKey(experience => experience.Id);

        builder.Property(experience => experience.Id).IsRequired();
        builder.Property(experience => experience.PersonId).IsRequired();
        builder.Property(experience => experience.SectorId).IsRequired();
        builder.Property(experience => experience.ExperienceMonths).IsRequired(false);
        builder.Property(experience => experience.Notes).HasMaxLength(1000).IsRequired(false);

        builder.HasIndex(experience => new
        {
            experience.PersonId,
            experience.SectorId
        }).IsUnique();

        builder.HasOne(experience => experience.Person)
            .WithMany(person => person.PersonSectorExperiences)
            .HasForeignKey(experience => experience.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(experience => experience.Sector)
            .WithMany(sector => sector.PersonSectorExperiences)
            .HasForeignKey(experience => experience.SectorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
