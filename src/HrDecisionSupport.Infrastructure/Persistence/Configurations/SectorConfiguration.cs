using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class SectorConfiguration : IEntityTypeConfiguration<Sector>
{
    public void Configure(EntityTypeBuilder<Sector> builder)
    {
        builder.ToTable("sectors");
        builder.HasKey(sector => sector.Id);

        builder.Property(sector => sector.Id).IsRequired();
        builder.Property(sector => sector.Code).HasMaxLength(50).IsRequired();
        builder.Property(sector => sector.Name).HasMaxLength(150).IsRequired();

        builder.HasIndex(sector => sector.Code).IsUnique();
        builder.UseSnakeCaseColumns();
    }
}
