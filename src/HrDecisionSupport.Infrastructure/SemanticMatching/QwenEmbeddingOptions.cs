namespace HrDecisionSupport.Infrastructure.SemanticMatching;

/// <summary>
/// Configuration options for the Qwen embedding HTTP service.
/// Bind from: SemanticMatching:EmbeddingService
/// </summary>
public sealed class QwenEmbeddingOptions
{
    public const string SectionName = "SemanticMatching:EmbeddingService";

    /// <summary>Base URL of the Python Qwen embedding service, e.g. http://127.0.0.1:8765</summary>
    public string BaseUrl { get; set; } = "http://127.0.0.1:8765";

    /// <summary>HTTP request timeout in seconds. CPU inference may take several seconds.</summary>
    public int TimeoutSeconds { get; set; } = 120;

    /// <summary>Expected embedding dimension. Used for output validation.</summary>
    public int ExpectedDimension { get; set; } = 1024;
}
