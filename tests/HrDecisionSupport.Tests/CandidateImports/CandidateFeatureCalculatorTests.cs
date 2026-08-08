using HrDecisionSupport.Application.CandidateImports.Processing;
using Xunit;

namespace HrDecisionSupport.Tests.CandidateImports;

public class CandidateFeatureCalculatorTests
{
    private readonly CandidateFeatureCalculator _calculator = new();

    [Fact]
    public void CalculateFeatures_ValidHistory_CalculatesCorrectly()
    {
        var result = _calculator.CalculateFeatures(
            5m,
            "Backend Developer ; Fullstack Developer",
            "Tech A ; Tech B",
            "01.2020-01.2022 ; 02.2022-02.2025");

        Assert.Equal(2, result.HistoryItems.Count);
        
        Assert.Equal(24, result.HistoryItems[0].DurationMonths); // 01.2020 to 01.2022
        Assert.Equal(36, result.HistoryItems[1].DurationMonths); // 02.2022 to 02.2025

        Assert.Equal(2, result.CompanyChangeCount);
        Assert.Equal(30, result.PreviousCompanyAverageStayMonths);
        Assert.Equal(24, result.ShortestPreviousJobMonths);
        Assert.Equal(36, result.LongestPreviousJobMonths);
        Assert.Equal(36, result.LastPreviousCompanyStayMonths); // The latest one chronologically

        Assert.Equal(0.4m, result.JobChangeRate); // 2 / 5
    }

    [Fact]
    public void CalculateFeatures_EmptyHistory_HandlesGracefully()
    {
        var result = _calculator.CalculateFeatures(0, null, null, null);

        Assert.Empty(result.HistoryItems);
        Assert.Equal(0, result.CompanyChangeCount);
        Assert.Null(result.PreviousCompanyAverageStayMonths);
        Assert.Equal(0, result.JobChangeRate);
    }
}
