using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class EducationRecordConfiguration : IEntityTypeConfiguration<EducationRecord>
{
    public void Configure(EntityTypeBuilder<EducationRecord> builder)
    {
        builder.ToTable(
            "education_records",
            table => table.HasCheckConstraint(
                "ck_education_records_graduation_date_not_before_start_date",
                "start_date IS NULL OR graduation_date IS NULL OR graduation_date >= start_date"));
        builder.HasKey(education => education.Id);

        builder.Property(education => education.Id).IsRequired();
        builder.Property(education => education.PersonId).IsRequired();
        builder.Property(education => education.Institution).HasMaxLength(250).IsRequired(false);
        builder.Property(education => education.FieldOfStudy).HasMaxLength(200).IsRequired(false);
        builder.Property(education => education.DegreeLevel).HasConversion<int>().IsRequired();
        builder.Property(education => education.StartDate).IsRequired(false);
        builder.Property(education => education.GraduationDate).IsRequired(false);

        builder.HasIndex(education => education.PersonId);
        builder.HasOne(education => education.Person)
            .WithMany(person => person.EducationRecords)
            .HasForeignKey(education => education.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
