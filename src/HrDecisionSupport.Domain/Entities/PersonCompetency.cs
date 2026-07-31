using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class PersonCompetency
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public Guid CompetencyId { get; set; }
    public int? ExperienceMonths { get; set; }
    public ProficiencyLevel? ProficiencyLevel { get; set; }

    public Person Person { get; set; } = null!;
    public Competency Competency { get; set; } = null!;
}
