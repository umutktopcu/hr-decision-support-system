using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.PositionBenchmarks;

public enum PositionBenchmarkSampleSizeStatus
{
    Unavailable = 1,
    LimitedData = 2,
    Sufficient = 3
}

public sealed record BenchmarkProfileCoverage(
    int TotalEmployees,
    int KnownProfiles)
{
    public int MissingProfiles => TotalEmployees - KnownProfiles;
}

public sealed record PositionBenchmarkResult(
    Guid PositionId,
    string PositionCode,
    string PositionName,
    int TotalEmployees,
    PositionBenchmarkSampleSizeStatus SampleSizeStatus,
    PositionWorkforceBenchmark Benchmark,
    PositionRequirementSuggestions Suggestions);

public sealed record PositionWorkforceBenchmark(
    PositionSkillBenchmark Skills,
    PositionExperienceBenchmark Experience,
    PositionEducationBenchmark Education,
    PositionLanguageBenchmark Languages);

public sealed record PositionSkillBenchmark(
    BenchmarkProfileCoverage Coverage,
    IReadOnlyList<PositionSkillFrequency> Items);

public sealed record PositionSkillFrequency(
    Guid CompetencyId,
    string CompetencyCode,
    string CompetencyName,
    CompetencyCategory CompetencyCategory,
    int EmployeeCount,
    int KnownProfileCount,
    decimal Percentage);

public sealed record PositionExperienceBenchmark(
    bool IsSupported,
    BenchmarkProfileCoverage Coverage,
    decimal? MedianMonths);

public sealed record PositionEducationBenchmark(
    BenchmarkProfileCoverage Coverage,
    IReadOnlyList<PositionEducationDistribution> Distribution);

public sealed record PositionEducationDistribution(
    DegreeLevel DegreeLevel,
    int EmployeeCount,
    int KnownProfileCount,
    decimal Percentage,
    int AtOrAboveEmployeeCount,
    decimal AtOrAbovePercentage);

public sealed record PositionLanguageBenchmark(
    BenchmarkProfileCoverage Coverage,
    IReadOnlyList<PositionLanguageFrequency> Items);

public sealed record PositionLanguageFrequency(
    Guid LanguageId,
    string LanguageCode,
    string LanguageName,
    int EmployeeCount,
    int KnownProfileCount,
    decimal Percentage,
    int KnownProficiencyCount,
    int NativeSpeakerCount,
    decimal? MedianProficiencyRank);

public sealed record PositionRequirementSuggestions(
    IReadOnlyList<PositionCompetencySuggestion> PreferredCompetencies,
    int? MinimumRelevantExperienceMonths,
    DegreeLevel? MinimumEducationLevel,
    IReadOnlyList<PositionLanguageSuggestion> Languages);

public sealed record PositionCompetencySuggestion(
    Guid CompetencyId,
    string CompetencyCode,
    string CompetencyName,
    bool IsRequired);

public sealed record PositionLanguageSuggestion(
    Guid LanguageId,
    string LanguageCode,
    string LanguageName,
    LanguageProficiencyLevel MinimumProficiency,
    bool HardFilterEnabled);
