using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Domain.Entities;
using System.Linq;

namespace HrDecisionSupport.Application.PreScreening.Helpers;

public static class LanguageRankHelper
{
    public static int GetProficiencyRank(LanguageProficiencyLevel level)
    {
        return level switch
        {
            LanguageProficiencyLevel.A1 => 1,
            LanguageProficiencyLevel.A2 => 2,
            LanguageProficiencyLevel.B1 => 3,
            LanguageProficiencyLevel.B2 => 4,
            LanguageProficiencyLevel.C1 => 5,
            LanguageProficiencyLevel.C2 => 6,
            _ => 0
        };
    }

    public static int GetCandidateProficiencyRank(PersonLanguage personLanguage)
    {
        if (personLanguage == null) return 0;

        // Native is considered the highest proficiency, conceptually above C2.
        if (personLanguage.IsNative)
            return 7;

        if (personLanguage.ProficiencyLevel.HasValue)
            return GetProficiencyRank(personLanguage.ProficiencyLevel.Value);

        return 0;
    }
}
