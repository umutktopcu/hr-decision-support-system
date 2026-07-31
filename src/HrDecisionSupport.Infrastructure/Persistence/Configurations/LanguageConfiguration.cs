using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class LanguageConfiguration : IEntityTypeConfiguration<Language>
{
    public void Configure(EntityTypeBuilder<Language> builder)
    {
        builder.ToTable("languages");
        builder.HasKey(language => language.Id);

        builder.Property(language => language.Id).IsRequired();
        builder.Property(language => language.Code).HasMaxLength(20).IsRequired();
        builder.Property(language => language.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(language => language.Code).IsUnique();
        builder.UseSnakeCaseColumns();
    }
}
