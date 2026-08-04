using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Profiles.Languages;

public sealed record CreatePersonLanguageRequest(
    Guid PersonId,
    Guid LanguageId,
    ProficiencyLevel ProficiencyLevel,
    bool IsNative);
