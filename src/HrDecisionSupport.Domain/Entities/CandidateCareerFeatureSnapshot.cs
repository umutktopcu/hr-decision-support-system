namespace HrDecisionSupport.Domain.Entities;

public class CandidateCareerFeatureSnapshot
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public int? TotalExperienceMonths { get; set; }
    public int? BackendExperienceMonths { get; set; }
    public decimal? PreviousCompanyAverageStayMonths { get; set; }
    public int? ShortestPreviousJobMonths { get; set; }
    public int? LongestPreviousJobMonths { get; set; }
    public int? LastPreviousCompanyStayMonths { get; set; }
    public int? CompanyChangeCount { get; set; }
    public decimal? JobChangeRate { get; set; }
    public string FeatureSchemaVersion { get; set; } = null!;
    public DateTime CalculatedAtUtc { get; set; }

    public Candidate Candidate { get; set; } = null!;
}
