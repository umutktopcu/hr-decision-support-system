using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class PersonWorkModeExperienceConfiguration : IEntityTypeConfiguration<PersonWorkModeExperience>
{
    public void Configure(EntityTypeBuilder<PersonWorkModeExperience> builder)
    {
        builder.ToTable(
            "person_work_mode_experiences",
            table => table.HasCheckConstraint(
                "ck_person_work_mode_experiences_experience_months_non_negative",
                "experience_months IS NULL OR experience_months >= 0"));
        builder.HasKey(experience => experience.Id);

        builder.Property(experience => experience.Id).IsRequired();
        builder.Property(experience => experience.PersonId).IsRequired();
        builder.Property(experience => experience.WorkModeId).IsRequired();
        builder.Property(experience => experience.ExperienceMonths).IsRequired(false);

        builder.HasIndex(experience => new
        {
            experience.PersonId,
            experience.WorkModeId
        }).IsUnique();

        builder.HasOne(experience => experience.Person)
            .WithMany(person => person.PersonWorkModeExperiences)
            .HasForeignKey(experience => experience.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(experience => experience.WorkMode)
            .WithMany(workMode => workMode.PersonWorkModeExperiences)
            .HasForeignKey(experience => experience.WorkModeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
