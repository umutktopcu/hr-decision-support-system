using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class PersonCompetencyConfiguration : IEntityTypeConfiguration<PersonCompetency>
{
    public void Configure(EntityTypeBuilder<PersonCompetency> builder)
    {
        builder.ToTable(
            "person_competencies",
            table => table.HasCheckConstraint(
                "ck_person_competencies_experience_months_non_negative",
                "experience_months IS NULL OR experience_months >= 0"));
        builder.HasKey(personCompetency => personCompetency.Id);

        builder.Property(personCompetency => personCompetency.Id).IsRequired();
        builder.Property(personCompetency => personCompetency.PersonId).IsRequired();
        builder.Property(personCompetency => personCompetency.CompetencyId).IsRequired();
        builder.Property(personCompetency => personCompetency.ExperienceMonths).IsRequired(false);
        builder.Property(personCompetency => personCompetency.ProficiencyLevel)
            .HasConversion<int>()
            .IsRequired(false);

        builder.HasIndex(personCompetency => new
        {
            personCompetency.PersonId,
            personCompetency.CompetencyId
        }).IsUnique();

        builder.HasOne(personCompetency => personCompetency.Person)
            .WithMany(person => person.PersonCompetencies)
            .HasForeignKey(personCompetency => personCompetency.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(personCompetency => personCompetency.Competency)
            .WithMany(competency => competency.PersonCompetencies)
            .HasForeignKey(personCompetency => personCompetency.CompetencyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
