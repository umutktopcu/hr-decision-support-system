namespace HrDecisionSupport.Domain.Entities;

public class PersonSectorExperience
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Guid SectorId { get; set; }
    public int? ExperienceMonths { get; set; }
    public string? Notes { get; set; }

    public Person Person { get; set; } = null!;
    public Sector Sector { get; set; } = null!;
}
