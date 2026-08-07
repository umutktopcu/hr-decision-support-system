namespace HrDecisionSupport.Domain.Entities;

public class CandidateWorkModePreference
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public Guid WorkModeId { get; set; }

    public Candidate Candidate { get; set; } = null!;
    public WorkMode WorkMode { get; set; } = null!;
}
