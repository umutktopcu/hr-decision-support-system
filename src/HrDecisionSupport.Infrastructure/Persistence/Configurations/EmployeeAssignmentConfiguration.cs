using HrDecisionSupport.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrDecisionSupport.Infrastructure.Persistence.Configurations;

public class EmployeeAssignmentConfiguration : IEntityTypeConfiguration<EmployeeAssignment>
{
    public void Configure(EntityTypeBuilder<EmployeeAssignment> builder)
    {
        builder.ToTable(
            "employee_assignments",
            table => table.HasCheckConstraint(
                "ck_employee_assignments_end_date_not_before_start_date",
                "end_date IS NULL OR end_date >= start_date"));
        builder.HasKey(assignment => assignment.Id);

        builder.Property(assignment => assignment.Id).IsRequired();
        builder.Property(assignment => assignment.EmployeeId).IsRequired();
        builder.Property(assignment => assignment.DepartmentId).IsRequired();
        builder.Property(assignment => assignment.PositionId).IsRequired();
        builder.Property(assignment => assignment.StartDate).IsRequired();
        builder.Property(assignment => assignment.EndDate).IsRequired(false);

        builder.HasIndex(assignment => assignment.EmployeeId);
        builder.HasIndex(assignment => assignment.DepartmentId);
        builder.HasIndex(assignment => assignment.PositionId);

        builder.HasOne(assignment => assignment.Employee)
            .WithMany(employee => employee.Assignments)
            .HasForeignKey(assignment => assignment.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.Department)
            .WithMany(department => department.EmployeeAssignments)
            .HasForeignKey(assignment => assignment.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(assignment => assignment.Position)
            .WithMany(position => position.EmployeeAssignments)
            .HasForeignKey(assignment => assignment.PositionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.UseSnakeCaseColumns();
    }
}
