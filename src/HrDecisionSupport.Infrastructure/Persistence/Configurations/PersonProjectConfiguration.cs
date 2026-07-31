using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class PersonProjectConfiguration : IEntityTypeConfiguration<PersonProject>
{
    public void Configure(EntityTypeBuilder<PersonProject> builder)
    {
        builder.ToTable(
            "person_projects",
            table => table.HasCheckConstraint(
                "ck_person_projects_end_date_not_before_start_date",
                "start_date IS NULL OR end_date IS NULL OR end_date >= start_date"));
        builder.HasKey(personProject => personProject.Id);

        builder.Property(personProject => personProject.Id).IsRequired();
        builder.Property(personProject => personProject.PersonId).IsRequired();
        builder.Property(personProject => personProject.ProjectId).IsRequired();
        builder.Property(personProject => personProject.Role).HasMaxLength(200).IsRequired(false);
        builder.Property(personProject => personProject.StartDate).IsRequired(false);
        builder.Property(personProject => personProject.EndDate).IsRequired(false);
        builder.Property(personProject => personProject.Description).HasMaxLength(2000).IsRequired(false);

        builder.HasIndex(personProject => personProject.PersonId);
        builder.HasIndex(personProject => personProject.ProjectId);

        builder.HasOne(personProject => personProject.Person)
            .WithMany(person => person.PersonProjects)
            .HasForeignKey(personProject => personProject.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(personProject => personProject.Project)
            .WithMany(project => project.PersonProjects)
            .HasForeignKey(personProject => personProject.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
