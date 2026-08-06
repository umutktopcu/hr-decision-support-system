using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class EmployeeImportRow
{
    public Guid Id { get; set; }
    public Guid ImportBatchId { get; set; }
    public int SourceRowNumber { get; set; }
    public string? ExternalEmployeeCode { get; set; }
    public string RawPayloadJson { get; set; } = null!;
    public EmployeeImportRowStatus ImportStatus { get; set; }
    public string? ValidationErrorsJson { get; set; }
    public Guid? EmployeeId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public EmployeeImportBatch ImportBatch { get; set; } = null!;
    public Employee? Employee { get; set; }
    public ICollection<PersonPriorPositionEvidence> PriorPositionEvidences { get; set; } = [];
}
