using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class CompetencyConfiguration : IEntityTypeConfiguration<Competency>
{
    public void Configure(EntityTypeBuilder<Competency> builder)
    {
        builder.ToTable("competencies");
        builder.HasKey(competency => competency.Id);

        builder.Property(competency => competency.Id).IsRequired();
        builder.Property(competency => competency.Code).HasMaxLength(50).IsRequired();
        builder.Property(competency => competency.Name).HasMaxLength(200).IsRequired();
        builder.Property(competency => competency.CompetencyCategory).HasConversion<int>().IsRequired();
        builder.Property(competency => competency.IsActive).IsRequired();

        builder.HasIndex(competency => competency.Code).IsUnique();
        builder.UseSnakeCaseColumns();
    }
}
