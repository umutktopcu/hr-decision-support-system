using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class CandidateEvaluationCase
{
    public Guid Id { get; set; }
    public Guid CandidateId { get; set; }
    public Guid JobRequisitionId { get; set; }
    public string? ExternalReference { get; set; }
    public DateTime ReceivedAtUtc { get; set; }
    public CandidateEvaluationStatus Status { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public Candidate Candidate { get; set; } = null!;
    public JobRequisition JobRequisition { get; set; } = null!;
}
