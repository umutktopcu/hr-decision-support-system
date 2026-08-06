using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class JobRequisitionRequirement
{
    public Guid Id { get; set; }
    public Guid JobRequisitionId { get; set; }
    public Guid CompetencyId { get; set; }
    public int? MinimumExperienceMonths { get; set; }
    public CompetencyProficiencyLevel? MinimumProficiencyLevel { get; set; }
    public bool IsRequired { get; set; }
    public string? Notes { get; set; }

    public JobRequisition JobRequisition { get; set; } = null!;
    public Competency Competency { get; set; } = null!;
}
