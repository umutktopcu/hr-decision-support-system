using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class Competency
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public CompetencyCategory CompetencyCategory { get; set; }
    public bool IsActive { get; set; }

    public ICollection<PersonCompetency> PersonCompetencies { get; set; } = [];
    public ICollection<JobRequisitionRequirement> JobRequisitionRequirements { get; set; } = [];
}
