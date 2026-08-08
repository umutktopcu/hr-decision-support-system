namespace HrDecisionSupport.Infrastructure.SemanticMatching.Reranking;

public class QwenRerankerOptions
{
    public const string SectionName = "SemanticMatching:RerankerService";

    public string BaseUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
}
