using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Domain.Entities;

public class JobRequisition
{
    public Guid Id { get; set; }
    public string RequisitionCode { get; set; } = null!;
    public string Title { get; set; } = null!;
    public Guid DepartmentId { get; set; }
    public Guid PositionId { get; set; }
    public string? Description { get; set; }
    public int OpeningsCount { get; set; }
    public JobRequisitionStatus JobRequisitionStatus { get; set; }
    public DateOnly OpenedAt { get; set; }
    public DateOnly? ClosedAt { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }

    public Department Department { get; set; } = null!;
    public Position Position { get; set; } = null!;
    public ICollection<JobRequisitionRequirement> Requirements { get; set; } = [];
    public ICollection<CandidateEvaluationCase> CandidateEvaluationCases { get; set; } = [];
}
