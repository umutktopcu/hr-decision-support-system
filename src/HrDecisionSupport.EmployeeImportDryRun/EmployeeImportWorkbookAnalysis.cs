using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Infrastructure.EmployeeImports;

namespace HrDecisionSupport.EmployeeImportDryRun;

/// <summary>Read-only diagnostics for a workbook. This type never writes to the workbook or a database.</summary>
public static class EmployeeImportWorkbookAnalysis
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<EmployeeImportWorkbookAnalysisReport> AnalyzeAsync(string workbookPath, DateOnly temporaryObservationDate, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(workbookPath);
        var reader = new ClosedXmlEmployeeSpreadsheetReader();
        await using var stream = File.OpenRead(workbookPath);
        var read = await reader.ReadAsync(stream, cancellationToken);
        if (read.IsFailure) throw new InvalidOperationException(read.Error!.Message);

        using var workbook = new XLWorkbook(workbookPath);
        var worksheet = workbook.Worksheets.First(sheet => sheet.Visibility == XLWorksheetVisibility.Visible && sheet.CellsUsed().Any());
        var headerRow = worksheet.FirstRowUsed()!;
        var lastColumn = worksheet.LastColumnUsed()!.ColumnNumber();
        var averageStayColumn = FindColumn(headerRow, lastColumn, EmployeeImportSpreadsheetHeaders.PreviousCompanyAverageStayMonths);
        var samples = new List<AverageStayCellSample>();
        var lastRow = worksheet.LastRowUsed()!.RowNumber();
        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var row = worksheet.Row(rowNumber);
            if (row.Cells(1, lastColumn).All(cell => string.IsNullOrWhiteSpace(cell.GetFormattedString()))) continue;
            samples.Add(ToAverageStaySample(rowNumber, worksheet.Cell(rowNumber, averageStayColumn)));
        }

        var invalidDecimalRows = read.Value.Rows
            .SelectMany(row => row.Diagnostics)
            .Where(diagnostic => diagnostic.Code == "invalid_decimal_value" && diagnostic.PropertyName == EmployeeImportSpreadsheetHeaders.PreviousCompanyAverageStayMonths)
            .Select(diagnostic => diagnostic.SourceRowNumber!.Value)
            .ToHashSet();
        var normalized = new EmployeeImportRowNormalizer();
        var occurrences = read.Value.Rows
            .SelectMany(row => normalized.Normalize(row).Diagnostics
                .Where(diagnostic => diagnostic.Code == "unknown_competency")
                .Select(diagnostic => new UnknownCompetencyOccurrence(diagnostic.PropertyName ?? string.Empty, row.SourceRowNumber)))
            .Where(occurrence => occurrence.Token.Length > 0)
            .ToArray();

        return new(
            new(Path.GetFileName(workbookPath), worksheet.Name, read.Value.Rows.Count, lastColumn, headerRow.Cells(1, lastColumn).Select(cell => cell.GetFormattedString()).ToArray(), true),
            BuildAverageStayAnalysis(samples, invalidDecimalRows),
            BuildUnknownCompetencyAnalysis(occurrences),
            temporaryObservationDate,
            true,
            "Do not change the production parser from this analysis alone. Review fractional month values and approved competency mappings separately.");
    }

    public static AverageStayAnalysis BuildAverageStayAnalysis(IEnumerable<AverageStayCellSample> samples, IReadOnlySet<int>? invalidDecimalRows = null)
    {
        var values = samples.ToArray();
        var parsed = values.Where(sample => sample.ParsedValue.HasValue).Select(sample => sample.ParsedValue!.Value).Order().ToArray();
        var fractional = values.Where(sample => sample.ParsedValue is decimal value && decimal.Truncate(value) != value).ToArray();
        var classifications = Enum.GetValues<AverageStayValueClass>().ToDictionary(value => value.ToString(), value => values.Count(sample => sample.Classification == value));
        var precision = fractional.GroupBy(sample => DecimalScale(sample.ParsedValue!.Value)).OrderBy(group => group.Key).ToDictionary(group => group.Key.ToString(CultureInfo.InvariantCulture), group => group.Count());
        var topFractionals = fractional.GroupBy(sample => new { sample.ParsedValue, sample.CellType })
            .OrderByDescending(group => group.Count()).ThenBy(group => group.Key.ParsedValue).Take(20)
            .Select(group => new FractionalAverageStayValue(group.Key.ParsedValue!.Value.ToString("G29", CultureInfo.InvariantCulture), group.Key.CellType, group.Count(), group.Select(sample => sample.SourceRowNumber).Take(20).ToArray()))
            .ToArray();
        var diagnosticCount = invalidDecimalRows?.Count ?? values.Count(sample => sample.Classification == AverageStayValueClass.InvalidText);
        return new(values.Length, classifications, values.Count(sample => sample.CellType == "Number"), values.Count(sample => sample.CellType == "Text"), values.Count(sample => sample.ParsedValue is decimal value && value < 0), values.Count(sample => sample.ParsedValue is decimal value && value == 0), parsed.Length == 0 ? null : parsed[0], parsed.Length == 0 ? null : parsed[^1], parsed.Length == 0 ? null : Median(parsed), parsed.Length == 0 ? null : parsed.Average(), precision, topFractionals, diagnosticCount);
    }

    public static UnknownCompetencyAnalysis BuildUnknownCompetencyAnalysis(IEnumerable<UnknownCompetencyOccurrence> occurrences)
    {
        var values = occurrences.Where(value => !string.IsNullOrWhiteSpace(value.Token)).ToArray();
        var tokens = values.GroupBy(value => NormalizeToken(value.Token), StringComparer.OrdinalIgnoreCase)
            .Select(group =>
            {
                var normalized = group.Key;
                var phrase = normalized.Any(char.IsWhiteSpace);
                return new UnknownCompetencyAggregate(group.First().Token, normalized, group.Count(), group.Select(value => value.SourceRowNumber).Distinct().Count(), group.Select(value => value.SourceRowNumber).Distinct().Order().Take(5).ToArray(), false, false, phrase ? "DescriptionOrPhraseReview" : "ManualReview", null, null, null, "Low", "No existing canonical or alias match was produced by the current normalizer; no semantic mapping was applied.");
            })
            .OrderByDescending(token => token.OccurrenceCount).ThenBy(token => token.NormalizedToken, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new(values.Length, tokens.Length, tokens.Take(100).ToArray(), tokens.Count(token => token.ProposedAction == "DescriptionOrPhraseReview"), tokens.Count(token => token.ProposedAction == "ManualReview"));
    }

    public static async Task WriteAsync(EmployeeImportWorkbookAnalysisReport report, string outputDirectory, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "analysis-summary.json"), JsonSerializer.Serialize(report, JsonOptions), cancellationToken);
        await WriteCsvAsync(Path.Combine(outputDirectory, "fractional-average-stay-values.csv"), ["ParsedValue", "CellType", "OccurrenceCount", "SampleSourceRows"], report.PreviousCompanyAverageStay.TopFractionalValues.Select(value => new[] { value.Value, value.CellType, value.OccurrenceCount.ToString(CultureInfo.InvariantCulture), string.Join("|", value.SampleSourceRows) }), cancellationToken);
        var rows = report.UnknownCompetencies.Top100;
        var headers = new[] { "RawToken", "NormalizedToken", "OccurrenceCount", "UniqueRowCount", "SampleSourceRows", "ExistingCanonicalMatch", "ExistingAliasMatch", "ProposedAction", "ProposedCanonicalCode", "ProposedCanonicalName", "ProposedCategory", "Confidence", "Notes" };
        var csvRows = rows.Select(row => new[] { row.RawToken, row.NormalizedToken, row.OccurrenceCount.ToString(CultureInfo.InvariantCulture), row.UniqueRowCount.ToString(CultureInfo.InvariantCulture), string.Join("|", row.SampleSourceRows), row.ExistingCanonicalMatch.ToString(), row.ExistingAliasMatch.ToString(), row.ProposedAction, row.ProposedCanonicalCode ?? string.Empty, row.ProposedCanonicalName ?? string.Empty, row.ProposedCategory ?? string.Empty, row.Confidence, row.Notes });
        await WriteCsvAsync(Path.Combine(outputDirectory, "unknown-competencies.csv"), headers, csvRows, cancellationToken);
        await WriteCsvAsync(Path.Combine(outputDirectory, "proposed-competency-decisions.csv"), headers, csvRows, cancellationToken);
    }

    public static AverageStayCellSample ClassifyAverageStayCell(int sourceRowNumber, string cellType, string formattedValue, decimal? numericValue = null)
    {
        var trimmed = formattedValue.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) && numericValue is null) return new(sourceRowNumber, cellType, formattedValue, AverageStayValueClass.Empty, null);
        if (numericValue is decimal numeric) return new(sourceRowNumber, cellType, formattedValue, decimal.Truncate(numeric) == numeric ? AverageStayValueClass.NumericInteger : AverageStayValueClass.NumericFractional, numeric);
        if (!TryParseDecimal(trimmed, out var parsed)) return new(sourceRowNumber, cellType, formattedValue, AverageStayValueClass.InvalidText, null);
        if (decimal.Truncate(parsed) == parsed) return new(sourceRowNumber, cellType, formattedValue, AverageStayValueClass.TextInteger, parsed);
        return new(sourceRowNumber, cellType, formattedValue, trimmed.Contains(',', StringComparison.Ordinal) ? AverageStayValueClass.CommaDecimalText : AverageStayValueClass.DotDecimalText, parsed);
    }

    private static AverageStayCellSample ToAverageStaySample(int sourceRowNumber, IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank) return ClassifyAverageStayCell(sourceRowNumber, "Blank", cell.GetFormattedString());
        if (value.IsNumber) return ClassifyAverageStayCell(sourceRowNumber, "Number", cell.GetFormattedString(), (decimal)value.GetNumber());
        return ClassifyAverageStayCell(sourceRowNumber, value.IsText ? "Text" : value.Type.ToString(), cell.GetFormattedString());
    }

    private static int FindColumn(IXLRow headerRow, int lastColumn, string requiredHeader)
    {
        foreach (var cell in headerRow.Cells(1, lastColumn))
            if (NormalizeHeader(cell.GetFormattedString()) == requiredHeader) return cell.Address.ColumnNumber;
        throw new InvalidOperationException($"The required header '{requiredHeader}' was not found after the reader accepted the workbook.");
    }

    private static bool TryParseDecimal(string value, out decimal parsed) => decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out parsed) || decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, TurkishCulture, out parsed);
    private static string NormalizeHeader(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormC).Trim(), "\\s+", " ");
    private static string NormalizeToken(string value) => Regex.Replace(value.Normalize(NormalizationForm.FormC).Trim(), "\\s+", " ");
    private static int DecimalScale(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0x7F;
    private static decimal Median(IReadOnlyList<decimal> values) => values.Count % 2 == 1 ? values[values.Count / 2] : (values[values.Count / 2 - 1] + values[values.Count / 2]) / 2m;
    private static async Task WriteCsvAsync(string path, IEnumerable<string> header, IEnumerable<IEnumerable<string>> rows, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(',', header.Select(Escape)));
        foreach (var row in rows) builder.AppendLine(string.Join(',', row.Select(Escape)));
        await File.WriteAllTextAsync(path, builder.ToString(), new UTF8Encoding(false), cancellationToken);
    }
    private static string Escape(string value) => '"' + value.Replace("\"", "\"\"") + '"';
}

public enum AverageStayValueClass { Empty, NumericInteger, NumericFractional, TextInteger, CommaDecimalText, DotDecimalText, InvalidText }
public sealed record AverageStayCellSample(int SourceRowNumber, string CellType, string FormattedValue, AverageStayValueClass Classification, decimal? ParsedValue);
public sealed record FractionalAverageStayValue(string Value, string CellType, int OccurrenceCount, IReadOnlyList<int> SampleSourceRows);
public sealed record AverageStayAnalysis(int TotalDataRows, IReadOnlyDictionary<string, int> Classifications, int NumericCellCount, int TextCellCount, int NegativeCount, int ZeroCount, decimal? Min, decimal? Max, decimal? Median, decimal? Average, IReadOnlyDictionary<string, int> FractionalPrecisionDistribution, IReadOnlyList<FractionalAverageStayValue> TopFractionalValues, int InvalidDecimalDiagnosticCount);
public sealed record UnknownCompetencyOccurrence(string Token, int SourceRowNumber);
public sealed record UnknownCompetencyAggregate(string RawToken, string NormalizedToken, int OccurrenceCount, int UniqueRowCount, IReadOnlyList<int> SampleSourceRows, bool ExistingCanonicalMatch, bool ExistingAliasMatch, string ProposedAction, string? ProposedCanonicalCode, string? ProposedCanonicalName, string? ProposedCategory, string Confidence, string Notes);
public sealed record UnknownCompetencyAnalysis(int TotalOccurrenceCount, int DistinctTokenCount, IReadOnlyList<UnknownCompetencyAggregate> Top100, int DescriptionOrPhraseReviewCount, int ManualReviewCount);
public sealed record WorkbookAnalysisMetadata(string FileName, string WorksheetName, int DataRowCount, int ColumnCount, IReadOnlyList<string> Headers, bool ReadOnly);
public sealed record EmployeeImportWorkbookAnalysisReport(WorkbookAnalysisMetadata Workbook, AverageStayAnalysis PreviousCompanyAverageStay, UnknownCompetencyAnalysis UnknownCompetencies, DateOnly TemporaryObservationDate, bool UsesTemporaryObservationDate, string RecommendedNextStep);
