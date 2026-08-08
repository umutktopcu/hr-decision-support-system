using HrDecisionSupport.Application.PreScreening.Evaluators;
using HrDecisionSupport.Application.PreScreening.Models;
using HrDecisionSupport.Application.PreScreening.Policy;

namespace HrDecisionSupport.Tests;

public class MandatorySkillFilterTests
{
    [Fact]
    public void Evaluate_ReturnsPass_WhenCoverageIsAboveThreshold()
    {
        var candidate = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
        var mandatory = new[] { candidate[0], candidate[1], Guid.NewGuid() }; // 2 out of 3 matched = 66%
        
        var result = MandatorySkillFilter.Evaluate(candidate, mandatory, 0.50m);
        
        Assert.True(result.Passed);
        Assert.Equal(EvaluationStatus.Pass, result.Status);
        Assert.Equal(3, result.TotalRequired);
        Assert.Equal(2, result.TotalMatched);
    }
    
    [Fact]
    public void Evaluate_ReturnsFail_WhenCoverageIsBelowThreshold()
    {
        var candidate = new[] { Guid.NewGuid() };
        var mandatory = new[] { candidate[0], Guid.NewGuid(), Guid.NewGuid() }; // 1 out of 3 = 33%
        
        var result = MandatorySkillFilter.Evaluate(candidate, mandatory, 0.50m);
        
        Assert.False(result.Passed);
        Assert.Equal(EvaluationStatus.Fail, result.Status);
    }
    
    [Fact]
    public void Evaluate_ReturnsPass_WhenExactlyOnThreshold()
    {
        var candidate = new[] { Guid.NewGuid() };
        var mandatory = new[] { candidate[0], Guid.NewGuid() }; // 1 out of 2 = 50%
        
        var result = MandatorySkillFilter.Evaluate(candidate, mandatory, 0.50m);
        
        Assert.True(result.Passed);
        Assert.Equal(0.50m, result.Coverage);
    }
    
    [Fact]
    public void Evaluate_ReturnsNotApplicable_WhenNoMandatoryRequirements()
    {
        var candidate = new[] { Guid.NewGuid() };
        var mandatory = Array.Empty<Guid>();
        
        var result = MandatorySkillFilter.Evaluate(candidate, mandatory, 0.50m);
        
        Assert.True(result.Passed); // Non-blocking
        Assert.Equal(EvaluationStatus.NotApplicable, result.Status);
        Assert.Equal(0, result.TotalRequired);
    }
    
    [Fact]
    public void Evaluate_HandlesDuplicates_UsingDistinctSets()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        
        var candidate = new[] { id1, id1, id2 };
        var mandatory = new[] { id1, id2, id2, Guid.NewGuid() }; // distinct required: 3. distinct matched: 2. = 66%
        
        var result = MandatorySkillFilter.Evaluate(candidate, mandatory, 0.50m);
        
        Assert.True(result.Passed);
        Assert.Equal(3, result.TotalRequired);
        Assert.Equal(2, result.TotalMatched);
    }
}
