namespace HrDecisionSupport.Infrastructure.SemanticMatching.Reranking;

public class QwenRerankerOptions
{
    public const string SectionName = "SemanticMatching:RerankerService";

    public string ModelName { get; set; } = "Qwen/Qwen3-Reranker-0.6B";
    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}
