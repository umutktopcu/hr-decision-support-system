using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class PersonConfiguration : IEntityTypeConfiguration<Person>
{
    public void Configure(EntityTypeBuilder<Person> builder)
    {
        builder.ToTable("people");
        builder.HasKey(person => person.Id);

        builder.Property(person => person.Id).IsRequired();
        builder.Property(person => person.AnonymousCode).HasMaxLength(100).IsRequired();
        builder.Property(person => person.FirstName).HasMaxLength(100).IsRequired(false);
        builder.Property(person => person.LastName).HasMaxLength(100).IsRequired(false);
        builder.Property(person => person.Email).HasMaxLength(320).IsRequired(false);
        builder.Property(person => person.PhoneNumber).HasMaxLength(30).IsRequired(false);
        builder.Property(person => person.CreatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired();
        builder.Property(person => person.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone")
            .IsRequired(false);

        builder.HasIndex(person => person.AnonymousCode).IsUnique();
        builder.HasIndex(person => person.Email);
        builder.UseSnakeCaseColumns();
    }
}
