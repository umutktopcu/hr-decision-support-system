using HrDecisionSupport.Application.PreScreening.Evaluators;
using HrDecisionSupport.Application.PreScreening.Models;
using HrDecisionSupport.Application.PreScreening.Policy;

namespace HrDecisionSupport.Tests;

public class OverallSkillFilterTests
{
    [Fact]
    public void Evaluate_ReturnsPass_WhenCoverageIsAboveThreshold()
    {
        var candidate = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var all = new[] { candidate[0], candidate[1], Guid.NewGuid() }; // 2 out of 3 matched = 66%
        
        var result = OverallSkillFilter.Evaluate(candidate, all, 0.50m);
        
        Assert.True(result.Passed);
        Assert.Equal(EvaluationStatus.Pass, result.Status);
    }
    
    [Fact]
    public void Evaluate_ReturnsFail_WhenCoverageIsBelowThreshold()
    {
        var candidate = new[] { Guid.NewGuid() };
        var all = new[] { candidate[0], Guid.NewGuid(), Guid.NewGuid() }; // 1 out of 3 = 33%
        
        var result = OverallSkillFilter.Evaluate(candidate, all, 0.50m);
        
        Assert.False(result.Passed);
        Assert.Equal(EvaluationStatus.Fail, result.Status);
    }
}
