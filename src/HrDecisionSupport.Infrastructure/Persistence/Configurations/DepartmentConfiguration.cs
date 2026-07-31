using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("departments");
        builder.HasKey(department => department.Id);

        builder.Property(department => department.Id).IsRequired();
        builder.Property(department => department.Code).HasMaxLength(50).IsRequired();
        builder.Property(department => department.Name).HasMaxLength(200).IsRequired();
        builder.Property(department => department.Description).HasMaxLength(1000).IsRequired(false);
        builder.Property(department => department.IsActive).IsRequired();

        builder.HasIndex(department => department.Code).IsUnique();
        builder.UseSnakeCaseColumns();
    }
}
