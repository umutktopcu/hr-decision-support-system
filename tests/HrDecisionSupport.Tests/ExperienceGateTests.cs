using HrDecisionSupport.Application.PreScreening.Evaluators;
using HrDecisionSupport.Application.PreScreening.Models;

namespace HrDecisionSupport.Tests;

public class ExperienceGateTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData(36, null, true)]
    [InlineData(null, 0, true)]
    [InlineData(null, -1, true)]
    public void Evaluate_ReturnsNotApplicableAndPass_WhenNoValidRequirement(
        int? candidate, int? required, bool expectedPassed)
    {
        var result = ExperienceGate.Evaluate(candidate, required);
        
        Assert.Equal(expectedPassed, result.Passed);
        Assert.Equal(EvaluationStatus.NotApplicable, result.Status);
    }
    
    [Theory]
    [InlineData(36, 24, true, EvaluationStatus.Pass)]
    [InlineData(24, 24, true, EvaluationStatus.Pass)]
    [InlineData(23, 24, false, EvaluationStatus.Fail)]
    [InlineData(null, 24, false, EvaluationStatus.Fail)]
    public void Evaluate_ReturnsCorrectResult_WhenRequirementExists(
        int? candidate, int? required, bool expectedPassed, EvaluationStatus expectedStatus)
    {
        var result = ExperienceGate.Evaluate(candidate, required);
        
        Assert.Equal(expectedPassed, result.Passed);
        Assert.Equal(expectedStatus, result.Status);
        Assert.Equal(candidate, result.CandidateRelevantExperienceMonths);
        Assert.Equal(required, result.RequiredRelevantExperienceMonths);
    }
}
