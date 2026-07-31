namespace HrDecisionSupport.Domain.Entities;

public class Position
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    public ICollection<EmployeeAssignment> EmployeeAssignments { get; set; } = [];
    public ICollection<JobRequisition> JobRequisitions { get; set; } = [];
}
