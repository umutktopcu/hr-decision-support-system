using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Requisitions;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.Requisitions.Requirements;

public sealed class CreateJobRequisitionRequirementRequestValidator
    : IValidator<CreateJobRequisitionRequirementRequest>
{
    public ValidationResult Validate(CreateJobRequisitionRequirementRequest instance)
    {
        ArgumentNullException.ThrowIfNull(instance);
        var errors = new List<ValidationError>();
        JobRequisitionValidation.RequiredGuid(
            errors,
            instance.JobRequisitionId,
            nameof(instance.JobRequisitionId),
            "job_requisition_id_required");
        JobRequisitionValidation.RequiredGuid(
            errors,
            instance.CompetencyId,
            nameof(instance.CompetencyId),
            "competency_id_required");
        JobRequisitionRequirementValidation.ValidateMetadata(
            errors,
            instance.MinimumExperienceMonths,
            instance.MinimumProficiencyLevel,
            instance.Notes);
        return RequestValidation.ToResult(errors);
    }
}

internal static class JobRequisitionRequirementValidation
{
    internal static void ValidateMetadata(
        ICollection<ValidationError> errors,
        int? minimumExperienceMonths,
        ProficiencyLevel? minimumProficiencyLevel,
        string? notes)
    {
        if (minimumExperienceMonths < 0)
        {
            errors.Add(new(
                "minimum_experience_months_negative",
                "MinimumExperienceMonths cannot be negative.",
                "MinimumExperienceMonths"));
        }

        if (minimumProficiencyLevel.HasValue
            && !Enum.IsDefined(minimumProficiencyLevel.Value))
        {
            errors.Add(new(
                "minimum_proficiency_level_invalid",
                "MinimumProficiencyLevel must be a defined value.",
                "MinimumProficiencyLevel"));
        }

        RequestValidation.OptionalString(errors, notes, 1000, "Notes", "notes");
    }
}
