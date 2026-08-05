using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Languages;

public sealed record UpdatePersonLanguageRequest(ProficiencyLevel ProficiencyLevel, bool IsNative);
