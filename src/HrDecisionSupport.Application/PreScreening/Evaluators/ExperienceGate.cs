using HrDecisionSupport.Application.PreScreening.Models;

namespace HrDecisionSupport.Application.PreScreening.Evaluators;

public static class ExperienceGate
{
    public static ExperienceEvaluationResult Evaluate(
        int? candidateRelevantExperienceMonths,
        int? minimumRelevantExperienceMonths)
    {
        if (minimumRelevantExperienceMonths is null or <= 0)
        {
            return new ExperienceEvaluationResult(
                CandidateRelevantExperienceMonths: candidateRelevantExperienceMonths,
                RequiredRelevantExperienceMonths: minimumRelevantExperienceMonths,
                Status: EvaluationStatus.NotApplicable,
                Passed: true); // Non-blocking if no requirement
        }

        var passed = candidateRelevantExperienceMonths >= minimumRelevantExperienceMonths;

        return new ExperienceEvaluationResult(
            CandidateRelevantExperienceMonths: candidateRelevantExperienceMonths,
            RequiredRelevantExperienceMonths: minimumRelevantExperienceMonths,
            Status: passed ? EvaluationStatus.Pass : EvaluationStatus.Fail,
            Passed: passed);
    }
}
