using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class PersonLanguageConfiguration : IEntityTypeConfiguration<PersonLanguage>
{
    public void Configure(EntityTypeBuilder<PersonLanguage> builder)
    {
        builder.ToTable("person_languages");
        builder.HasKey(personLanguage => personLanguage.Id);

        builder.Property(personLanguage => personLanguage.Id).IsRequired();
        builder.Property(personLanguage => personLanguage.PersonId).IsRequired();
        builder.Property(personLanguage => personLanguage.LanguageId).IsRequired();
        builder.Property(personLanguage => personLanguage.ProficiencyLevel)
            .HasConversion<int>()
            .IsRequired(false);
        builder.Property(personLanguage => personLanguage.IsNative).IsRequired();

        builder.HasIndex(personLanguage => new
        {
            personLanguage.PersonId,
            personLanguage.LanguageId
        }).IsUnique();

        builder.HasOne(personLanguage => personLanguage.Person)
            .WithMany(person => person.PersonLanguages)
            .HasForeignKey(personLanguage => personLanguage.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(personLanguage => personLanguage.Language)
            .WithMany(language => language.PersonLanguages)
            .HasForeignKey(personLanguage => personLanguage.LanguageId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
