using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class EmploymentHistoryConfiguration : IEntityTypeConfiguration<EmploymentHistory>
{
    public void Configure(EntityTypeBuilder<EmploymentHistory> builder)
    {
        builder.ToTable(
            "employment_histories",
            table => table.HasCheckConstraint(
                "ck_employment_histories_end_date_not_before_start_date",
                "end_date IS NULL OR end_date >= start_date"));
        builder.HasKey(history => history.Id);

        builder.Property(history => history.Id).IsRequired();
        builder.Property(history => history.PersonId).IsRequired();
        builder.Property(history => history.EmployerName).HasMaxLength(200).IsRequired();
        builder.Property(history => history.PositionTitle).HasMaxLength(200).IsRequired();
        builder.Property(history => history.StartDate).IsRequired();
        builder.Property(history => history.EndDate).IsRequired(false);
        builder.Property(history => history.Description).HasMaxLength(2000).IsRequired(false);

        builder.HasIndex(history => history.PersonId);
        builder.HasOne(history => history.Person)
            .WithMany(person => person.EmploymentHistories)
            .HasForeignKey(history => history.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
