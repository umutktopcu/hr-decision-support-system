using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class Candidate
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string CandidateCode { get; set; } = null!;
    public CandidateSource CandidateSource { get; set; }
    public string? ExternalCandidateId { get; set; }
    public string? ProfessionalTitle { get; set; }
    public int? AvailabilityDays { get; set; }

    public Person Person { get; set; } = null!;
    public ICollection<CandidateEvaluationCase> EvaluationCases { get; set; } = [];
    public ICollection<CandidateWorkModePreference> WorkModePreferences { get; set; } = [];
    public ICollection<CandidateCareerFeatureSnapshot> CareerFeatureSnapshots { get; set; } = [];
}
