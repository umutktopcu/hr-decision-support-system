using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class PersonCertificateConfiguration : IEntityTypeConfiguration<PersonCertificate>
{
    public void Configure(EntityTypeBuilder<PersonCertificate> builder)
    {
        builder.ToTable(
            "person_certificates",
            table => table.HasCheckConstraint(
                "ck_person_certificates_expiration_date_not_before_issue_date",
                "issue_date IS NULL OR expiration_date IS NULL OR expiration_date >= issue_date"));
        builder.HasKey(personCertificate => personCertificate.Id);

        builder.Property(personCertificate => personCertificate.Id).IsRequired();
        builder.Property(personCertificate => personCertificate.PersonId).IsRequired();
        builder.Property(personCertificate => personCertificate.CertificateId).IsRequired();
        builder.Property(personCertificate => personCertificate.IssueDate).IsRequired(false);
        builder.Property(personCertificate => personCertificate.ExpirationDate).IsRequired(false);
        builder.Property(personCertificate => personCertificate.CredentialCode)
            .HasMaxLength(200)
            .IsRequired(false);

        builder.HasIndex(personCertificate => new
        {
            personCertificate.PersonId,
            personCertificate.CertificateId
        });

        builder.HasOne(personCertificate => personCertificate.Person)
            .WithMany(person => person.PersonCertificates)
            .HasForeignKey(personCertificate => personCertificate.PersonId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(personCertificate => personCertificate.Certificate)
            .WithMany(certificate => certificate.PersonCertificates)
            .HasForeignKey(personCertificate => personCertificate.CertificateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
