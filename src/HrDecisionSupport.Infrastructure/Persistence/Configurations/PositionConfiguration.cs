using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.ToTable("positions");
        builder.HasKey(position => position.Id);

        builder.Property(position => position.Id).IsRequired();
        builder.Property(position => position.Code).HasMaxLength(50).IsRequired();
        builder.Property(position => position.Name).HasMaxLength(200).IsRequired();
        builder.Property(position => position.Description).HasMaxLength(1000).IsRequired(false);
        builder.Property(position => position.IsActive).IsRequired();

        builder.HasIndex(position => position.Code).IsUnique();
        builder.UseSnakeCaseColumns();
    }
}
