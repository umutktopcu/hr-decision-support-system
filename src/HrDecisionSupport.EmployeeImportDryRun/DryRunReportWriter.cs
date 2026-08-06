using System.Globalization;
using System.Text;
using System.Text.Json;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.EmployeeImportDryRun;

public static class DryRunReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static EmployeeImportDryRunReport Build(EmployeeImportDryRunResult result, string fileName, TimeSpan elapsed, TimeSpan readerElapsed, long memoryStartBytes, long memoryEndBytes)
    {
        var rows = result.Rows;
        var diagnostics = rows.SelectMany(x => x.Diagnostics).ToArray();
        var aggregate = diagnostics.GroupBy(x => new { x.Code, x.Severity }).OrderByDescending(x => x.Count()).ThenBy(x => x.Key.Code).Select(group => new DiagnosticAggregate(group.Key.Code, group.Key.Severity.ToString(), group.Count(), group.Select(x => x.SourceRowNumber).Where(x => x.HasValue).Select(x => x!.Value).Distinct().Count(), group.Select(x => x.SourceRowNumber).Where(x => x.HasValue).Select(x => x!.Value).Distinct().Take(5).ToArray(), group.Select(x => x.PropertyName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).Take(3).Cast<string>().ToArray(), group.Select(x => x.Message).Distinct(StringComparer.Ordinal).Take(3).ToArray(), Group(group.Key.Code))).ToArray();
        return new EmployeeImportDryRunReport(fileName, result.WorksheetName, result.TotalRows, result.ValidRows, result.ValidWithWarningsRows, result.InvalidRows, result.ErrorCount, result.WarningCount, result.TotalDiagnostics, result.CompetencyCount, result.ProjectEvidenceCount, result.SectorExperienceCount, result.CertificateCount, result.LanguageCount, result.WorkModeCount, elapsed.TotalMilliseconds, readerElapsed.TotalMilliseconds, Math.Max(0, elapsed.TotalMilliseconds - readerElapsed.TotalMilliseconds), memoryStartBytes, memoryEndBytes, aggregate, new LabelDistribution(rows.Count(x => x.NormalizedRow.StayLabel == EmployeeRetentionLabelValue.Short), rows.Count(x => x.NormalizedRow.StayLabel == EmployeeRetentionLabelValue.Normal), rows.Count(x => x.NormalizedRow.StayLabel == EmployeeRetentionLabelValue.Long), rows.Count(x => x.NormalizedRow.StayLabel is null)), Distribution(rows.Select(x => x.NormalizedRow.ShortestPreviousJobMonths)), Distribution(rows.Select(x => x.NormalizedRow.LongestPreviousJobMonths)), rows.Count(x => x.NormalizedRow.ShortestPreviousJobMonths is not null && x.NormalizedRow.LongestPreviousJobMonths is not null && x.NormalizedRow.ShortestPreviousJobMonths > x.NormalizedRow.LongestPreviousJobMonths), rows.Count(x => x.NormalizedRow.ShortestPreviousJobMonths == 0 && x.NormalizedRow.LongestPreviousJobMonths == 0));
    }

    public static async Task WriteAsync(EmployeeImportDryRunReport report, EmployeeImportDryRunResult result, string outputDirectory, CancellationToken ct)
    {
        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "summary.json"), JsonSerializer.Serialize(report, JsonOptions), ct);
        await WriteCsvAsync(Path.Combine(outputDirectory, "diagnostics.csv"), ["DiagnosticCode", "Severity", "Count", "UniqueRowCount", "SampleRows", "SampleTokens", "SampleMessages", "Group"], report.Diagnostics.Select(x => new[] { x.Code, x.Severity, x.Count.ToString(CultureInfo.InvariantCulture), x.UniqueRowCount.ToString(CultureInfo.InvariantCulture), string.Join("|", x.SampleRows), string.Join("|", x.SampleTokens), string.Join("|", x.SampleMessages), x.Group }), ct);
        await WriteRowsAsync(Path.Combine(outputDirectory, "invalid-rows.csv"), result.Rows.Where(x => x.Status == EmployeeImportValidationStatus.Invalid), ct);
        await WriteRowsAsync(Path.Combine(outputDirectory, "warning-rows.csv"), result.Rows.Where(x => x.Status == EmployeeImportValidationStatus.ValidWithWarnings), ct);
    }

    private static async Task WriteRowsAsync(string path, IEnumerable<EmployeeImportDryRunRowResult> rows, CancellationToken ct) => await WriteCsvAsync(path, ["SourceRowNumber", "EmployeeCode", "Status", "DiagnosticCodes", "DiagnosticMessages"], rows.Select(x => new[] { x.SourceRowNumber.ToString(CultureInfo.InvariantCulture), Mask(x.EmployeeCode), x.Status.ToString(), string.Join("|", x.Diagnostics.Select(d => d.Code).Distinct()), string.Join("|", x.Diagnostics.Select(d => d.Message).Distinct()) }), ct);
    private static async Task WriteCsvAsync(string path, IEnumerable<string> header, IEnumerable<IEnumerable<string>> rows, CancellationToken ct)
    {
        var builder = new StringBuilder(); builder.AppendLine(string.Join(',', header.Select(Escape)));
        foreach (var row in rows) builder.AppendLine(string.Join(',', row.Select(Escape)));
        await File.WriteAllTextAsync(path, builder.ToString(), new UTF8Encoding(false), ct);
    }
    private static string Escape(string value) => '"' + value.Replace("\"", "\"\"") + '"';
    private static string Mask(string? code) => string.IsNullOrWhiteSpace(code) ? string.Empty : code.Length <= 4 ? "***" : "***" + code[^4..];
    private static string Group(string code) => code.Contains("workbook", StringComparison.OrdinalIgnoreCase) || code.Contains("header", StringComparison.OrdinalIgnoreCase) ? "Workbook/schema" : code.Contains("duration", StringComparison.OrdinalIgnoreCase) || code.Contains("date", StringComparison.OrdinalIgnoreCase) || code.Contains("month", StringComparison.OrdinalIgnoreCase) ? "Numeric/date parse" : code.Contains("competency", StringComparison.OrdinalIgnoreCase) ? "Unknown competency" : code.Contains("sector", StringComparison.OrdinalIgnoreCase) ? "Sector parse" : code.Contains("education", StringComparison.OrdinalIgnoreCase) ? "Education mapping" : code.Contains("certificate", StringComparison.OrdinalIgnoreCase) ? "Certificate" : code.Contains("language", StringComparison.OrdinalIgnoreCase) ? "Language" : code.Contains("work_mode", StringComparison.OrdinalIgnoreCase) ? "WorkMode" : code.Contains("label", StringComparison.OrdinalIgnoreCase) ? "Retention label" : code.Contains("required", StringComparison.OrdinalIgnoreCase) || code.Contains("missing", StringComparison.OrdinalIgnoreCase) ? "Missing required fields" : "Cross-field validation";
    private static FeatureDistribution Distribution(IEnumerable<int?> values)
    {
        var list = values.Where(x => x.HasValue).Select(x => x!.Value).Order().ToArray(); return new(list.Length, values.Count() - list.Length, list.Length == 0 ? null : list[0], list.Length == 0 ? null : list[^1], list.Length == 0 ? null : Median(list), list.Count(x => x < 0));
    }
    private static double Median(IReadOnlyList<int> values) => values.Count % 2 == 1 ? values[values.Count / 2] : (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2d;
}

public sealed record EmployeeImportDryRunReport(string FileName, string WorksheetName, int TotalRows, int ValidRows, int ValidWithWarningsRows, int InvalidRows, int ErrorCount, int WarningCount, int TotalDiagnostics, int CompetencyCount, int ProjectEvidenceCount, int SectorExperienceCount, int CertificateCount, int LanguageCount, int WorkModeCount, double ElapsedMilliseconds, double ReaderElapsedMilliseconds, double NormalizeValidationElapsedMilliseconds, long MemoryStartBytes, long MemoryEndBytes, IReadOnlyList<DiagnosticAggregate> Diagnostics, LabelDistribution Labels, FeatureDistribution ShortestPreviousJobMonths, FeatureDistribution LongestPreviousJobMonths, int ShortestGreaterThanLongestCount, int ZeroOverZeroCount);
public sealed record DiagnosticAggregate(string Code, string Severity, int Count, int UniqueRowCount, IReadOnlyList<int> SampleRows, IReadOnlyList<string> SampleTokens, IReadOnlyList<string> SampleMessages, string Group);
public sealed record LabelDistribution(int Short, int Normal, int Long, int InvalidOrMissing);
public sealed record FeatureDistribution(int NonNullCount, int NullCount, int? Min, int? Max, double? Median, int NegativeCount);
