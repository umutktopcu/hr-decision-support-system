using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.SemanticMatching.Embeddings;

public interface IEmbeddingProvider
{
    /// <summary>
    /// Generates an embedding for a single query text.
    /// Implementations may apply model-specific query instructions internally.
    /// The returned vector is expected to be normalized.
    /// </summary>
    Task<Result<float[]>> GenerateQueryEmbeddingAsync(
        string queryText,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embeddings for a batch of document texts.
    /// Empty documents should not be passed to this method.
    /// The returned list will have the same count and order as the input list.
    /// Vectors are expected to be normalized.
    /// </summary>
    Task<Result<IReadOnlyList<float[]>>> GenerateDocumentEmbeddingsAsync(
        IReadOnlyList<string> documentTexts,
        CancellationToken cancellationToken = default);
}
