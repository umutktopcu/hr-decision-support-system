namespace HrDecisionSupport.Domain.Entities;

public class PersonPriorPositionEvidence
{
    public Guid Id { get; set; }
    public Guid PersonId { get; set; }
    public string Title { get; set; } = null!;
    public int SequenceNumber { get; set; }
    public Guid ImportRowId { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    public Person Person { get; set; } = null!;
    public EmployeeImportRow ImportRow { get; set; } = null!;
}
