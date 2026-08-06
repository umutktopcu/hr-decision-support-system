using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Languages;

public sealed record UpdatePersonLanguageRequest(LanguageProficiencyLevel ProficiencyLevel, bool IsNative);
