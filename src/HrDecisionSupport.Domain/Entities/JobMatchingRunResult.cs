using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public sealed class JobMatchingRunResult
{
    public Guid JobMatchingRunId { get; set; }
    public Guid CandidateId { get; set; }
    public string? CandidateCodeSnapshot { get; set; }
    public string CandidateDisplayNameSnapshot { get; set; } = null!;
    public int FinalRank { get; set; }
    public int SkillTier { get; set; }
    public decimal MandatorySkillCoverage { get; set; }
    public decimal PreferredSkillCoverage { get; set; }
    public double EmbeddingScore { get; set; }
    public double CrossEncoderRawScore { get; set; }
    public double JobFitScore { get; set; }
    public RetentionPredictionStatus RetentionPredictionStatus { get; set; }
    public EmployeeRetentionLabelValue? RetentionLabel { get; set; }
    public int? ShortestPreviousJobMonthsSnapshot { get; set; }
    public int? LongestPreviousJobMonthsSnapshot { get; set; }

    public JobMatchingRun JobMatchingRun { get; set; } = null!;
}
