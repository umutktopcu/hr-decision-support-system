using System.Text;
using HrDecisionSupport.EmployeeImportDryRun;

namespace HrDecisionSupport.Tests;

public sealed class EmployeeImportWorkbookAnalysisTests
{
    [Fact]
    public void BuildAverageStayAnalysis_ClassifiesNumericAndTextValuesWithPrecision()
    {
        var samples = new[]
        {
            EmployeeImportWorkbookAnalysis.ClassifyAverageStayCell(2, "Blank", ""),
            EmployeeImportWorkbookAnalysis.ClassifyAverageStayCell(3, "Number", "12", 12m),
            EmployeeImportWorkbookAnalysis.ClassifyAverageStayCell(4, "Number", "12.5", 12.5m),
            EmployeeImportWorkbookAnalysis.ClassifyAverageStayCell(5, "Text", "3,75"),
            EmployeeImportWorkbookAnalysis.ClassifyAverageStayCell(6, "Text", "4.125"),
            EmployeeImportWorkbookAnalysis.ClassifyAverageStayCell(7, "Text", "not-a-number"),
            EmployeeImportWorkbookAnalysis.ClassifyAverageStayCell(8, "Number", "-1", -1m),
            EmployeeImportWorkbookAnalysis.ClassifyAverageStayCell(9, "Number", "0", 0m)
        };

        var result = EmployeeImportWorkbookAnalysis.BuildAverageStayAnalysis(samples, new HashSet<int> { 4, 5, 6, 7 });

        Assert.Equal(1, result.Classifications[nameof(AverageStayValueClass.Empty)]);
        Assert.Equal(3, result.Classifications[nameof(AverageStayValueClass.NumericInteger)]);
        Assert.Equal(1, result.Classifications[nameof(AverageStayValueClass.NumericFractional)]);
        Assert.Equal(1, result.Classifications[nameof(AverageStayValueClass.CommaDecimalText)]);
        Assert.Equal(1, result.Classifications[nameof(AverageStayValueClass.DotDecimalText)]);
        Assert.Equal(1, result.Classifications[nameof(AverageStayValueClass.InvalidText)]);
        Assert.Equal(1, result.NegativeCount); Assert.Equal(1, result.ZeroCount); Assert.Equal(-1m, result.Min); Assert.Equal(12.5m, result.Max);
        Assert.Equal(4, result.InvalidIntegerDiagnosticCount);
        Assert.Equal(1, result.FractionalPrecisionDistribution["1"]); Assert.Equal(1, result.FractionalPrecisionDistribution["2"]); Assert.Equal(1, result.FractionalPrecisionDistribution["3"]);
    }

    [Fact]
    public void BuildUnknownCompetencyAnalysis_AggregatesRowsAndDoesNotProposeSemanticAliases()
    {
        var result = EmployeeImportWorkbookAnalysis.BuildUnknownCompetencyAnalysis([
            new("Asenkron programlama", 2), new("asenkron   programlama", 3), new("TekilToken", 4)
        ]);

        Assert.Equal(3, result.TotalOccurrenceCount); Assert.Equal(2, result.DistinctTokenCount);
        var phrase = Assert.Single(result.Top100, token => token.NormalizedToken == "Asenkron programlama");
        Assert.Equal(2, phrase.OccurrenceCount); Assert.Equal(2, phrase.UniqueRowCount); Assert.Equal([2, 3], phrase.SampleSourceRows);
        Assert.False(phrase.ExistingCanonicalMatch); Assert.False(phrase.ExistingAliasMatch); Assert.Equal("DescriptionOrPhraseReview", phrase.ProposedAction); Assert.Null(phrase.ProposedCanonicalCode); Assert.Equal("Low", phrase.Confidence);
    }

    [Fact]
    public async Task WriteAsync_WritesUtf8PrivacySafeArtifactsAndTemporaryDateMetadata()
    {
        var average = EmployeeImportWorkbookAnalysis.BuildAverageStayAnalysis([
            EmployeeImportWorkbookAnalysis.ClassifyAverageStayCell(8, "Number", "8.5", 8.5m)
        ], new HashSet<int> { 8 });
        var unknown = EmployeeImportWorkbookAnalysis.BuildUnknownCompetencyAnalysis([new("Veri tabanı tasarımı", 8)]);
        var report = new EmployeeImportWorkbookAnalysisReport(new("sample.xlsx", "Employees", 1, 24, ["Header"], true), average, unknown, new DateOnly(2026, 8, 6), true, "Review only.");
        var path = Path.Combine(Path.GetTempPath(), "hrds-analysis-" + Guid.NewGuid().ToString("N"));
        try
        {
            await EmployeeImportWorkbookAnalysis.WriteAsync(report, path, CancellationToken.None);
            var summary = await File.ReadAllTextAsync(Path.Combine(path, "analysis-summary.json"));
            var csvBytes = await File.ReadAllBytesAsync(Path.Combine(path, "unknown-competencies.csv"));
            var csv = Encoding.UTF8.GetString(csvBytes);
            Assert.Contains("2026-08-06", summary); Assert.Contains("Veri tabanı tasarımı", csv); Assert.DoesNotContain("EmployeeCode", csv); Assert.DoesNotContain("RawValues", csv); Assert.True(File.Exists(Path.Combine(path, "fractional-average-stay-values.csv"))); Assert.True(File.Exists(Path.Combine(path, "proposed-competency-decisions.csv")));
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }
}
