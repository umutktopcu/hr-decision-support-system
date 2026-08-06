using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class EmployeeRetentionLabel
{
    public Guid Id { get; set; }
    public Guid EmployeeCareerFeatureSnapshotId { get; set; }
    public EmployeeRetentionLabelValue Label { get; set; }
    public EmployeeRetentionLabelSource LabelSource { get; set; }
    public string LabelDefinitionVersion { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }

    public EmployeeCareerFeatureSnapshot EmployeeCareerFeatureSnapshot { get; set; } = null!;
}
