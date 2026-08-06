using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.EmployeeImportDryRun;

namespace HrDecisionSupport.Tests;

public sealed class EmployeeImportDryRunReportTests
{
    [Fact]
    public async Task Build_AggregatesDiagnosticsLabelsAndFeatureQuality()
    {
        var result = await DryRunAsync(); var report = DryRunReportWriter.Build(result, "sample.xlsx", TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(5), 10, 20);
        Assert.Equal("sample.xlsx", report.FileName); Assert.Equal(2, report.TotalRows); Assert.Equal(1, report.ValidRows); Assert.Equal(1, report.ValidWithWarningsRows); Assert.Equal(1, report.Labels.Short); Assert.Equal(1, report.Labels.Normal); Assert.Equal(1, report.ShortestPreviousJobMonths.NonNullCount); Assert.Equal(1, report.ShortestPreviousJobMonths.NullCount); Assert.Equal(6, report.ShortestPreviousJobMonths.Min); Assert.Equal(12, report.LongestPreviousJobMonths.Max);
        var diagnostic = Assert.Single(report.Diagnostics); Assert.Equal("unknown_competency", diagnostic.Code); Assert.Equal("Unknown competency", diagnostic.Group); Assert.Equal(1, diagnostic.UniqueRowCount); Assert.Equal([3], diagnostic.SampleRows);
    }

    [Fact]
    public async Task WriteAsync_ProducesSanitizedJsonAndCsvReports()
    {
        var result = await DryRunAsync(); var report = DryRunReportWriter.Build(result, "sample.xlsx", TimeSpan.Zero, TimeSpan.Zero, 0, 0); var path = Path.Combine(Path.GetTempPath(), "hrds-dry-run-" + Guid.NewGuid().ToString("N"));
        try
        {
            await DryRunReportWriter.WriteAsync(report, result, path, CancellationToken.None);
            Assert.True(File.Exists(Path.Combine(path, "summary.json"))); Assert.True(File.Exists(Path.Combine(path, "diagnostics.csv"))); Assert.True(File.Exists(Path.Combine(path, "invalid-rows.csv"))); var warnings = await File.ReadAllTextAsync(Path.Combine(path, "warning-rows.csv"));
            Assert.Contains("***N001", warnings); Assert.DoesNotContain("EmployeeCode,RawValues", warnings);
        }
        finally { if (Directory.Exists(path)) Directory.Delete(path, true); }
    }

    private static async Task<EmployeeImportDryRunResult> DryRunAsync()
    {
        var valid = Source(2, "VALID", "C#", 0, 6, 12); var warning = Source(3, "WARN001", "Unknown Thing", 1, null, null);
        var reader = new Reader([valid, warning]); var service = new EmployeeImportDryRunService(reader, new EmployeeImportRowNormalizer(), new EmployeeImportRowValidator()); var result = await service.DryRunAsync(Stream.Null, new(EmployeeDatasetSplit.Training, new DateOnly(2025, 1, 1))); Assert.True(result.IsSuccess); return result.Value;
    }
    private static EmployeeImportSourceRow Source(int row, string code, string skills, int label, int? shortest, int? longest) => new(row, code, "Backend", 5, 4, "Developer", skills, null, "Project", "ERP (12 ay)", "Lisans", "Computer Science", "AWS", "English C1", "Ofis", new DateOnly(2020, 1, 1), null, 5, 12, shortest, longest, 4, 1, .2m, label, new Dictionary<string, string?>(), []);
    private sealed class Reader(IReadOnlyList<EmployeeImportSourceRow> rows) : IEmployeeSpreadsheetReader { public Task<Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(Stream content, CancellationToken cancellationToken = default) => Task.FromResult(Result<EmployeeImportSpreadsheetReadResult>.Success(new("Employees", [], rows, []))); }
}
