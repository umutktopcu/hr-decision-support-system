using HrDecisionSupport.Application.Requisitions.Requirements;
using HrDecisionSupport.Domain.Enums;
using System.Collections.Generic;
using System;

namespace HrDecisionSupport.Application.Requisitions;

public sealed record JobRequisitionDetailDto(
    Guid Id,
    string RequisitionCode,
    string Title,
    Guid DepartmentId,
    string DepartmentCode,
    string DepartmentName,
    Guid PositionId,
    string PositionCode,
    string PositionName,
    string? Description,
    int OpeningsCount,
    int? MinimumRelevantExperienceMonths,
    DegreeLevel? MinimumEducationLevel,
    Guid? WorkModeId,
    bool WorkModeHardFilterEnabled,
    JobRequisitionStatus JobRequisitionStatus,
    DateOnly OpenedAt,
    DateOnly? ClosedAt,
    decimal? MandatorySkillCoverageThreshold,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int RequirementCount,
    IReadOnlyList<JobRequisitionRequirementDto> Requirements,
    IReadOnlyList<JobLanguageRequirementDto> LanguageRequirements);
