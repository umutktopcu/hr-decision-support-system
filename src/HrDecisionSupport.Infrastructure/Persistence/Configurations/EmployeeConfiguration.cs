using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.ToTable(
            "employees",
            table => table.HasCheckConstraint(
                "ck_employees_termination_date_not_before_hire_date",
                "termination_date IS NULL OR termination_date >= hire_date"));
        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.Id).IsRequired();
        builder.Property(employee => employee.PersonId).IsRequired();
        builder.Property(employee => employee.EmployeeCode).HasMaxLength(50).IsRequired();
        builder.Property(employee => employee.HireDate).IsRequired();
        builder.Property(employee => employee.TerminationDate).IsRequired(false);
        builder.Property(employee => employee.EmploymentStatus).HasConversion<int>().IsRequired();

        builder.HasIndex(employee => employee.PersonId).IsUnique();
        builder.HasIndex(employee => employee.EmployeeCode).IsUnique();

        builder.HasOne(employee => employee.Person)
            .WithOne(person => person.Employee)
            .HasForeignKey<Employee>(employee => employee.PersonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
