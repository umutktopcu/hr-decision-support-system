namespace HrDecisionSupport.Domain.Entities;

public class Project
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public ICollection<PersonProject> PersonProjects { get; set; } = [];
}
