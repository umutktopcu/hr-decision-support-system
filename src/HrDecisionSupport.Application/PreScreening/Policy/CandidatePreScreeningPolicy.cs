namespace HrDecisionSupport.Application.PreScreening.Policy;

public sealed record CandidatePreScreeningPolicy
{
    public decimal MandatorySkillCoverageThreshold { get; init; } = 0.50m;
    public decimal OverallSkillCoverageThreshold { get; init; } = 0.50m;
}
