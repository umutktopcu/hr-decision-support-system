using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class Employee
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string EmployeeCode { get; set; } = null!;
    public DateOnly HireDate { get; set; }
    public DateOnly? TerminationDate { get; set; }
    public EmploymentStatus EmploymentStatus { get; set; }

    public Person Person { get; set; } = null!;
    public ICollection<EmployeeAssignment> Assignments { get; set; } = [];
}
