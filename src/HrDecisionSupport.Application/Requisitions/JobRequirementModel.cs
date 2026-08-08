using HrDecisionSupport.Domain.Enums;
using System;

namespace HrDecisionSupport.Application.Requisitions;

public sealed record JobRequirementModel(
    Guid CompetencyId,
    int? MinimumExperienceMonths,
    CompetencyProficiencyLevel? MinimumProficiencyLevel,
    bool IsRequired,
    string? Notes);
