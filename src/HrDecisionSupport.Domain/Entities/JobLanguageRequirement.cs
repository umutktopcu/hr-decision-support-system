using System;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class JobLanguageRequirement
{
    public Guid Id { get; set; }
    public Guid JobRequisitionId { get; set; }
    public Guid LanguageId { get; set; }
    public LanguageProficiencyLevel MinimumProficiency { get; set; }
    public bool HardFilterEnabled { get; set; }

    public JobRequisition JobRequisition { get; set; } = null!;
    public Language Language { get; set; } = null!;
}
