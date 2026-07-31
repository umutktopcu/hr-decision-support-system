using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class CertificateConfiguration : IEntityTypeConfiguration<Certificate>
{
    public void Configure(EntityTypeBuilder<Certificate> builder)
    {
        builder.ToTable("certificates");
        builder.HasKey(certificate => certificate.Id);

        builder.Property(certificate => certificate.Id).IsRequired();
        builder.Property(certificate => certificate.Code).HasMaxLength(50).IsRequired();
        builder.Property(certificate => certificate.Name).HasMaxLength(250).IsRequired();
        builder.Property(certificate => certificate.Issuer).HasMaxLength(250).IsRequired(false);

        builder.HasIndex(certificate => certificate.Code).IsUnique();
        builder.UseSnakeCaseColumns();
    }
}
