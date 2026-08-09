using HrDecisionSupport.Domain.Enums;
using System;

namespace HrDecisionSupport.Application.Requisitions.Requirements;

public sealed record JobLanguageRequirementDto(
    Guid Id,
    Guid JobRequisitionId,
    Guid LanguageId,
    string LanguageName,
    LanguageProficiencyLevel MinimumProficiency,
    bool HardFilterEnabled);
