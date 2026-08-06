using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class EmployeeCareerFeatureSnapshot
{
    public Guid Id { get; set; }
    public Guid EmployeeId { get; set; }
    public Guid ImportBatchId { get; set; }
    public DateOnly? ObservedAt { get; set; }
    public int? TotalExperienceMonths { get; set; }
    public int? BackendExperienceMonths { get; set; }
    public bool? HasPreviousCompany { get; set; }
    public decimal? PreviousCompanyAverageStayMonths { get; set; }
    public int? ShortestPreviousJobMonths { get; set; }
    public int? LongestPreviousJobMonths { get; set; }
    public int? LastPreviousCompanyStayMonths { get; set; }
    public int? CompanyChangeCount { get; set; }
    public decimal? ImportedJobChangeRate { get; set; }
    public int? ObservedCompanyTenureMonths { get; set; }
    public EmployeeCareerFeatureSource FeatureSource { get; set; }
    public string FeatureSchemaVersion { get; set; } = null!;
    public DateTime CreatedAtUtc { get; set; }

    public Employee Employee { get; set; } = null!;
    public EmployeeImportBatch ImportBatch { get; set; } = null!;
    public EmployeeRetentionLabel? RetentionLabel { get; set; }
}
