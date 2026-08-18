using Pgvector;

namespace HrDecisionSupport.Infrastructure.Persistence.Embeddings;

public sealed class JobRequisitionEmbedding
{
    public Guid JobRequisitionId { get; set; }
    public string DocumentHash { get; set; } = null!;
    public string ModelName { get; set; } = null!;
    public Vector EmbeddingVector { get; set; } = null!;
    public DateTime UpdatedAtUtc { get; set; }
}
