namespace HrDecisionSupport.Domain.Entities;

public sealed class JobMatchingRun
{
    public Guid Id { get; set; }
    public Guid JobRequisitionId { get; set; }
    public DateTime ExecutedAtUtc { get; set; }
    public string JobRequisitionCodeSnapshot { get; set; } = null!;
    public string JobTitleSnapshot { get; set; } = null!;
    public int RetrievalTopN { get; set; }
    public int FinalTopN { get; set; }
    public int CandidatePoolCount { get; set; }
    public int HardFilterPassedCount { get; set; }
    public int RetrievedCandidateCount { get; set; }
    public int FinalCandidateCount { get; set; }
    public string EmbeddingModelName { get; set; } = null!;
    public string RerankerModelName { get; set; } = null!;
    public string RetentionModelName { get; set; } = null!;
    public string RetentionFeatureSchemaVersion { get; set; } = null!;
    public string JobDocumentHash { get; set; } = null!;
    public string ConfigurationSnapshotJson { get; set; } = null!;

    public JobRequisition JobRequisition { get; set; } = null!;
    public ICollection<JobMatchingRunResult> Results { get; set; } = [];
}
