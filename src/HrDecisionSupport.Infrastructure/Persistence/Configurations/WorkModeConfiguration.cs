using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class WorkModeConfiguration : IEntityTypeConfiguration<WorkMode>
{
    public void Configure(EntityTypeBuilder<WorkMode> builder)
    {
        builder.ToTable("work_modes");
        builder.HasKey(workMode => workMode.Id);

        builder.Property(workMode => workMode.Id).IsRequired();
        builder.Property(workMode => workMode.Code).HasMaxLength(50).IsRequired();
        builder.Property(workMode => workMode.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(workMode => workMode.Code).IsUnique();
        builder.UseSnakeCaseColumns();
    }
}
