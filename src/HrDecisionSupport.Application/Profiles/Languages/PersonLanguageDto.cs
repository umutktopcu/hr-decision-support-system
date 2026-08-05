using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Languages;

public sealed record PersonLanguageDto(
    Guid Id,
    Guid PersonId,
    Guid LanguageId,
    string LanguageCode,
    string LanguageName,
    ProficiencyLevel ProficiencyLevel,
    bool IsNative);
