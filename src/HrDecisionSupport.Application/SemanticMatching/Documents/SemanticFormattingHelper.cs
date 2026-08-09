using System;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.SemanticMatching.Documents;

public static class SemanticFormattingHelper
{
    public static bool ShouldInclude(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    public static string? FormatDegreeLevel(DegreeLevel degreeLevel)
    {
        return degreeLevel switch
        {
            DegreeLevel.HighSchool => "Lise",
            DegreeLevel.Associate => "Önlisans",
            DegreeLevel.Bachelor => "Lisans",
            DegreeLevel.Master => "Yüksek Lisans",
            DegreeLevel.Doctorate => "Doktora",
            DegreeLevel.Other => "Diğer",
            _ => null
        };
    }

    public static string? FormatLanguageProficiency(LanguageProficiencyLevel level)
    {
        return level switch
        {
            LanguageProficiencyLevel.A1 => "A1",
            LanguageProficiencyLevel.A2 => "A2",
            LanguageProficiencyLevel.B1 => "B1",
            LanguageProficiencyLevel.B2 => "B2",
            LanguageProficiencyLevel.C1 => "C1",
            LanguageProficiencyLevel.C2 => "C2",
            _ => null
        };
    }
}
