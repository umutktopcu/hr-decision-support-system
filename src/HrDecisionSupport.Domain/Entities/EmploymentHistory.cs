namespace HrDecisionSupport.Domain.Entities;

public class EmploymentHistory
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string EmployerName { get; set; } = null!;
    public string PositionTitle { get; set; } = null!;
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public string? Description { get; set; }

    public Person Person { get; set; } = null!;
}
