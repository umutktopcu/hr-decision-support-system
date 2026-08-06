using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class EmployeeImportBatch
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = null!;
    public string FileHash { get; set; } = null!;
    public EmployeeDatasetSplit DatasetSplit { get; set; }
    public DateOnly? ObservationDate { get; set; }
    public DateTime ImportedAtUtc { get; set; }
    public int TotalRowCount { get; set; }
    public int SuccessfulRowCount { get; set; }
    public int FailedRowCount { get; set; }
    public EmployeeImportBatchStatus Status { get; set; }

    public ICollection<EmployeeImportRow> Rows { get; set; } = [];
    public ICollection<EmployeeCareerFeatureSnapshot> FeatureSnapshots { get; set; } = [];
}
