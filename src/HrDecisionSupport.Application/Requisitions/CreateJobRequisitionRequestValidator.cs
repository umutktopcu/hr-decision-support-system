using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;

namespace HrDecisionSupport.Application.Requisitions;

public sealed class CreateJobRequisitionRequestValidator
    : IValidator<CreateJobRequisitionRequest>
{
    public ValidationResult Validate(CreateJobRequisitionRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        JobRequisitionValidation.ValidateCommon(
            errors,
            instance.RequisitionCode,
            instance.Title,
            instance.DepartmentId,
            instance.PositionId,
            instance.Description,
            instance.OpeningsCount,
            instance.MinimumRelevantExperienceMonths,
            instance.OpenedAt,
            instance.MandatorySkillCoverageThreshold,
            instance.OverallSkillCoverageThreshold);
        return RequestValidation.ToResult(errors);
    }
}

internal static class JobRequisitionValidation
{
    internal static void ValidateCommon(
        ICollection<ValidationError> errors,
        string? requisitionCode,
        string? title,
        Guid departmentId,
        Guid positionId,
        string? description,
        int openingsCount,
        int? minimumRelevantExperienceMonths,
        DateOnly openedAt,
        decimal? mandatorySkillCoverageThreshold,
        decimal? overallSkillCoverageThreshold)
    {
        RequestValidation.RequiredString(
            errors, requisitionCode, 50, "RequisitionCode", "requisition_code");
        RequestValidation.RequiredString(errors, title, 250, "Title", "title");
        RequestValidation.OptionalString(
            errors, description, 2000, "Description", "description");

        RequiredGuid(errors, departmentId, "DepartmentId", "department_id_required");
        RequiredGuid(errors, positionId, "PositionId", "position_id_required");

        if (openingsCount <= 0)
        {
            errors.Add(new(
                "openings_count_invalid",
                "OpeningsCount must be greater than zero.",
                "OpeningsCount"));
        }

        if (minimumRelevantExperienceMonths < 0)
        {
            errors.Add(new(
                "min_relevant_experience_negative",
                "MinimumRelevantExperienceMonths cannot be negative.",
                "MinimumRelevantExperienceMonths"));
        }

        if (openedAt == default)
        {
            errors.Add(new(
                "opened_at_required",
                "OpenedAt must not be the default date.",
                "OpenedAt"));
        }

        if (mandatorySkillCoverageThreshold.HasValue && (mandatorySkillCoverageThreshold.Value < 0 || mandatorySkillCoverageThreshold.Value > 1))
        {
            errors.Add(new(
                "mandatory_threshold_invalid",
                "MandatorySkillCoverageThreshold must be between 0 and 1.",
                "MandatorySkillCoverageThreshold"));
        }

        if (overallSkillCoverageThreshold.HasValue && (overallSkillCoverageThreshold.Value < 0 || overallSkillCoverageThreshold.Value > 1))
        {
            errors.Add(new(
                "overall_threshold_invalid",
                "OverallSkillCoverageThreshold must be between 0 and 1.",
                "OverallSkillCoverageThreshold"));
        }
    }

    internal static void RequiredGuid(
        ICollection<ValidationError> errors,
        Guid value,
        string propertyName,
        string code)
    {
        if (value == Guid.Empty)
            errors.Add(new(code, $"{propertyName} must not be empty.", propertyName));
    }
}
