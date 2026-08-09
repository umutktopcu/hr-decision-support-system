using HrDecisionSupport.Application.PreScreening.Helpers;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Xunit;

namespace HrDecisionSupport.Tests.Application.PreScreening.Helpers;

public class LanguageRankHelperTests
{
    [Theory]
    [InlineData(LanguageProficiencyLevel.A1, 1)]
    [InlineData(LanguageProficiencyLevel.A2, 2)]
    [InlineData(LanguageProficiencyLevel.B1, 3)]
    [InlineData(LanguageProficiencyLevel.B2, 4)]
    [InlineData(LanguageProficiencyLevel.C1, 5)]
    [InlineData(LanguageProficiencyLevel.C2, 6)]
    public void GetProficiencyRank_ReturnsExpectedRank(LanguageProficiencyLevel level, int expectedRank)
    {
        var rank = LanguageRankHelper.GetProficiencyRank(level);
        Assert.Equal(expectedRank, rank);
    }

    [Fact]
    public void GetCandidateProficiencyRank_NullPersonLanguage_ReturnsZero()
    {
        var rank = LanguageRankHelper.GetCandidateProficiencyRank(null);
        Assert.Equal(0, rank);
    }

    [Fact]
    public void GetCandidateProficiencyRank_IsNative_ReturnsMaxRank()
    {
        var lang = new PersonLanguage { IsNative = true, ProficiencyLevel = LanguageProficiencyLevel.A1 };
        var rank = LanguageRankHelper.GetCandidateProficiencyRank(lang);
        Assert.Equal(7, rank); // Above C2
    }

    [Fact]
    public void GetCandidateProficiencyRank_NotNative_ReturnsProficiencyRank()
    {
        var lang = new PersonLanguage { IsNative = false, ProficiencyLevel = LanguageProficiencyLevel.B2 };
        var rank = LanguageRankHelper.GetCandidateProficiencyRank(lang);
        Assert.Equal(4, rank);
    }
}
