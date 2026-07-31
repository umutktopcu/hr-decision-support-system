using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("projects");
        builder.HasKey(project => project.Id);

        builder.Property(project => project.Id).IsRequired();
        builder.Property(project => project.Name).HasMaxLength(250).IsRequired();
        builder.Property(project => project.Description).HasMaxLength(2000).IsRequired(false);

        builder.UseSnakeCaseColumns();
    }
}
