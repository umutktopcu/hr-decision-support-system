namespace HrDecisionSupport.Domain.Entities;

public class WorkMode
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    public ICollection<PersonWorkModeExperience> PersonWorkModeExperiences { get; set; } = [];
    public ICollection<CandidateWorkModePreference> CandidateWorkModePreferences { get; set; } = [];
}
