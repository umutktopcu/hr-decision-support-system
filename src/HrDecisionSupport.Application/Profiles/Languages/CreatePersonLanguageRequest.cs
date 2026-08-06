using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Languages;

public sealed record CreatePersonLanguageRequest(
    Guid PersonId,
    Guid LanguageId,
    LanguageProficiencyLevel ProficiencyLevel,
    bool IsNative);
