using HrDecisionSupport.Application.PreScreening.Models;
using HrDecisionSupport.Application.PreScreening.Policy;

namespace HrDecisionSupport.Application.PreScreening.Evaluators;

public static class OverallSkillFilter
{
    public static SkillEvaluationResult Evaluate(
        IReadOnlyCollection<Guid> candidateCompetencyIds,
        IReadOnlyCollection<Guid> allJobRequirementCompetencyIds,
        decimal effectiveThreshold)
    {
        var required = allJobRequirementCompetencyIds.Distinct().ToArray();
        var candidate = candidateCompetencyIds.Distinct().ToArray();

        if (required.Length == 0)
        {
            return new SkillEvaluationResult(
                TotalRequired: 0,
                TotalMatched: 0,
                Coverage: 0m,
                Status: EvaluationStatus.NotApplicable,
                Passed: true, // Non-blocking
                MatchedCompetencyIds: Array.Empty<Guid>(),
                MissingCompetencyIds: Array.Empty<Guid>());
        }

        var matched = required.Intersect(candidate).ToArray();
        var missing = required.Except(matched).ToArray();

        var coverage = (decimal)matched.Length / required.Length;
        var passed = coverage >= effectiveThreshold;

        return new SkillEvaluationResult(
            TotalRequired: required.Length,
            TotalMatched: matched.Length,
            Coverage: coverage,
            Status: passed ? EvaluationStatus.Pass : EvaluationStatus.Fail,
            Passed: passed,
            MatchedCompetencyIds: matched,
            MissingCompetencyIds: missing);
    }
}
