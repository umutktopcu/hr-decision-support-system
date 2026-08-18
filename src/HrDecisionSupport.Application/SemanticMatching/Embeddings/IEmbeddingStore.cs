namespace HrDecisionSupport.Application.SemanticMatching.Embeddings;

public sealed record StoredEmbedding(
    Guid EntityId,
    string DocumentHash,
    string ModelName,
    float[] Vector,
    DateTime UpdatedAtUtc);

public interface IEmbeddingStore
{
    Task<IReadOnlyDictionary<Guid, StoredEmbedding>> GetCandidateEmbeddingsAsync(
        IReadOnlyCollection<Guid> candidateIds,
        CancellationToken cancellationToken = default);

    Task<StoredEmbedding?> GetJobRequisitionEmbeddingAsync(
        Guid jobRequisitionId,
        CancellationToken cancellationToken = default);

    Task UpsertCandidateEmbeddingsAsync(
        IReadOnlyCollection<StoredEmbedding> embeddings,
        CancellationToken cancellationToken = default);

    Task UpsertJobRequisitionEmbeddingAsync(
        StoredEmbedding embedding,
        CancellationToken cancellationToken = default);
}
