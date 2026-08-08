using HrDecisionSupport.Domain.Enums;
using System;

namespace HrDecisionSupport.Application.Requisitions;

public sealed record JobLanguageRequirementModel(
    Guid LanguageId,
    LanguageProficiencyLevel MinimumProficiency,
    bool HardFilterEnabled);
