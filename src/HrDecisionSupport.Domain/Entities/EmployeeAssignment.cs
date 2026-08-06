namespace HrDecisionSupport.Domain.Entities;

public class EmployeeAssignment
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid DepartmentId { get; set; }
    public Guid PositionId { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public Employee Employee { get; set; } = null!;
    public Department Department { get; set; } = null!;
    public Position Position { get; set; } = null!;
}
