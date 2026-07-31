namespace HrDecisionSupport.Domain.Entities;

public class PersonWorkModeExperience
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Guid WorkModeId { get; set; }
    public int? ExperienceMonths { get; set; }

    public Person Person { get; set; } = null!;
    public WorkMode WorkMode { get; set; } = null!;
}
