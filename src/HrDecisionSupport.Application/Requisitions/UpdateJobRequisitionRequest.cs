using HrDecisionSupport.Domain.Enums;
using System;
using System.Collections.Generic;

namespace HrDecisionSupport.Application.Requisitions;

public sealed record UpdateJobRequisitionRequest(
    string RequisitionCode,
    string Title,
    Guid DepartmentId,
    Guid PositionId,
    string? Description,
    int OpeningsCount,
    int? MinimumRelevantExperienceMonths = null,
    DateOnly OpenedAt = default,
    decimal? MandatorySkillCoverageThreshold = null,
    DegreeLevel? MinimumEducationLevel = null,
    Guid? WorkModeId = null,
    bool WorkModeHardFilterEnabled = false,
    IReadOnlyList<JobRequirementModel>? Requirements = null,
    IReadOnlyList<JobLanguageRequirementModel>? LanguageRequirements = null);
