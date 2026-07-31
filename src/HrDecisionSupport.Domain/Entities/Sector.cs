namespace HrDecisionSupport.Domain.Entities;

public class Sector
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    public ICollection<PersonSectorExperience> PersonSectorExperiences { get; set; } = [];
}
