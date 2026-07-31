namespace HrDecisionSupport.Domain.Entities;

public class PersonProject
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Guid ProjectId { get; set; }
    public string? Role { get; set; }
    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Description { get; set; }

    public Person Person { get; set; } = null!;
    public Project Project { get; set; } = null!;
}
