using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.SemanticMatching.Embeddings;

public interface IEmbeddingProvider
{
    /// <summary>
    /// Generates embeddings for a batch of text documents.
    /// Empty documents should not be passed to this method.
    /// The returned list will have the same count and order as the input list.
    /// Vectors are not guaranteed to be normalized.
    /// </summary>
    Task<Result<IReadOnlyList<float[]>>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}
