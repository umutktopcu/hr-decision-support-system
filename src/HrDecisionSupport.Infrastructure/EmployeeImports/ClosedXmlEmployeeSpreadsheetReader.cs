using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;

namespace HrDecisionSupport.Infrastructure.EmployeeImports;

public sealed class ClosedXmlEmployeeSpreadsheetReader : IEmployeeSpreadsheetReader
{
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly string[] DateFormats = ["dd.MM.yyyy", "d.M.yyyy", "yyyy-MM-dd"];

    public async Task<Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(
        Stream content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);
        cancellationToken.ThrowIfCancellationRequested();

        MemoryStream? copiedContent = null;
        try
        {
            var workbookContent = content;
            if (!content.CanSeek)
            {
                copiedContent = new MemoryStream();
                await content.CopyToAsync(copiedContent, cancellationToken);
                copiedContent.Position = 0;
                workbookContent = copiedContent;
            }

            using var workbook = new XLWorkbook(workbookContent);
            cancellationToken.ThrowIfCancellationRequested();

            var worksheet = workbook.Worksheets.FirstOrDefault(sheet =>
                sheet.Visibility == XLWorksheetVisibility.Visible && sheet.CellsUsed().Any());
            if (worksheet is null)
            {
                return Result<EmployeeImportSpreadsheetReadResult>.Failure(
                    "employee_spreadsheet.empty_worksheet",
                    "The workbook does not contain a visible, non-empty worksheet.");
            }

            var headerRow = worksheet.FirstRowUsed();
            var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber();
            if (headerRow is null || lastColumn is null)
            {
                return Result<EmployeeImportSpreadsheetReadResult>.Failure(
                    "employee_spreadsheet.missing_header_row",
                    "The selected worksheet does not contain a header row.");
            }

            var schemaDiagnostics = new List<EmployeeImportDiagnostic>();
            var headers = new List<string>();
            var columnsByHeader = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var cell in headerRow.Cells(1, lastColumn.Value))
            {
                var normalizedHeader = NormalizeHeader(GetVisibleText(cell));
                headers.Add(normalizedHeader);
                if (normalizedHeader.Length == 0)
                {
                    schemaDiagnostics.Add(new(
                        "empty_header",
                        null,
                        $"Column {cell.Address.ColumnLetter} has an empty header.",
                        EmployeeImportDiagnosticSeverity.Warning,
                        null));
                    continue;
                }

                if (!columnsByHeader.TryAdd(normalizedHeader, cell.Address.ColumnNumber))
                {
                    return Result<EmployeeImportSpreadsheetReadResult>.Failure(
                        "employee_spreadsheet.duplicate_header",
                        $"The header '{normalizedHeader}' appears more than once.");
                }

                if (!EmployeeImportSpreadsheetHeaders.Required.Contains(normalizedHeader, StringComparer.Ordinal))
                {
                    schemaDiagnostics.Add(new(
                        "unexpected_header",
                        normalizedHeader,
                        $"The header '{normalizedHeader}' is not part of the employee import schema.",
                        EmployeeImportDiagnosticSeverity.Warning,
                        null));
                }
            }

            var missingHeader = EmployeeImportSpreadsheetHeaders.Required
                .FirstOrDefault(header => !columnsByHeader.ContainsKey(header));
            if (missingHeader is not null)
            {
                return Result<EmployeeImportSpreadsheetReadResult>.Failure(
                    "employee_spreadsheet.missing_required_header",
                    $"The required header '{missingHeader}' is missing.");
            }

            var rows = new List<EmployeeImportSourceRow>();
            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();
            for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = worksheet.Row(rowNumber);
                if (row.Cells(1, lastColumn.Value).All(cell => string.IsNullOrWhiteSpace(GetVisibleText(cell))))
                {
                    continue;
                }

                rows.Add(CreateSourceRow(worksheet, rowNumber, columnsByHeader));
            }

            return Result<EmployeeImportSpreadsheetReadResult>.Success(new(
                worksheet.Name,
                headers.AsReadOnly(),
                rows.AsReadOnly(),
                schemaDiagnostics.AsReadOnly()));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Result<EmployeeImportSpreadsheetReadResult>.Failure(
                "employee_spreadsheet.unsupported_workbook",
                "The workbook could not be opened as a supported spreadsheet.");
        }
        finally
        {
            copiedContent?.Dispose();
        }
    }

    private static EmployeeImportSourceRow CreateSourceRow(
        IXLWorksheet worksheet,
        int sourceRowNumber,
        IReadOnlyDictionary<string, int> columnsByHeader)
    {
        var diagnostics = new List<EmployeeImportDiagnostic>();
        var rawValues = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var header in EmployeeImportSpreadsheetHeaders.Required)
        {
            rawValues[header] = GetVisibleText(worksheet.Cell(sourceRowNumber, columnsByHeader[header]));
        }

        IXLCell Cell(string header) => worksheet.Cell(sourceRowNumber, columnsByHeader[header]);
        string? Text(string header) => NormalizeText(rawValues[header] ?? string.Empty);

        var anonymousEmployeeCode = Text(EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode);
        if (anonymousEmployeeCode is null)
        {
            diagnostics.Add(new(
                "empty_employee_code",
                EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode,
                "Anonymous employee code is empty.",
                EmployeeImportDiagnosticSeverity.Error,
                sourceRowNumber));
        }

        return new(
            sourceRowNumber,
            anonymousEmployeeCode,
            Text(EmployeeImportSpreadsheetHeaders.CurrentPosition),
            ReadDecimal(Cell(EmployeeImportSpreadsheetHeaders.TotalExperience), EmployeeImportSpreadsheetHeaders.TotalExperience, sourceRowNumber, diagnostics),
            ReadDecimal(Cell(EmployeeImportSpreadsheetHeaders.BackendExperience), EmployeeImportSpreadsheetHeaders.BackendExperience, sourceRowNumber, diagnostics),
            Text(EmployeeImportSpreadsheetHeaders.PreviousPositions),
            Text(EmployeeImportSpreadsheetHeaders.TechnicalSkills),
            Text(EmployeeImportSpreadsheetHeaders.TechnologiesAndTools),
            Text(EmployeeImportSpreadsheetHeaders.ProjectExperiences),
            Text(EmployeeImportSpreadsheetHeaders.SectorExperience),
            Text(EmployeeImportSpreadsheetHeaders.EducationLevel),
            Text(EmployeeImportSpreadsheetHeaders.EducationField),
            Text(EmployeeImportSpreadsheetHeaders.Certificates),
            Text(EmployeeImportSpreadsheetHeaders.ForeignLanguages),
            Text(EmployeeImportSpreadsheetHeaders.WorkModeExperience),
            ReadDate(Cell(EmployeeImportSpreadsheetHeaders.HireDate), EmployeeImportSpreadsheetHeaders.HireDate, sourceRowNumber, diagnostics),
            ReadDate(Cell(EmployeeImportSpreadsheetHeaders.TerminationDate), EmployeeImportSpreadsheetHeaders.TerminationDate, sourceRowNumber, diagnostics),
            ReadDecimal(Cell(EmployeeImportSpreadsheetHeaders.CompanyTenureYears), EmployeeImportSpreadsheetHeaders.CompanyTenureYears, sourceRowNumber, diagnostics),
            ReadInteger(Cell(EmployeeImportSpreadsheetHeaders.PreviousCompanyAverageStayMonths), EmployeeImportSpreadsheetHeaders.PreviousCompanyAverageStayMonths, sourceRowNumber, diagnostics),
            ReadInteger(Cell(EmployeeImportSpreadsheetHeaders.ShortestPreviousJobMonths), EmployeeImportSpreadsheetHeaders.ShortestPreviousJobMonths, sourceRowNumber, diagnostics),
            ReadInteger(Cell(EmployeeImportSpreadsheetHeaders.LongestPreviousJobMonths), EmployeeImportSpreadsheetHeaders.LongestPreviousJobMonths, sourceRowNumber, diagnostics),
            ReadInteger(Cell(EmployeeImportSpreadsheetHeaders.LastPreviousCompanyStayMonths), EmployeeImportSpreadsheetHeaders.LastPreviousCompanyStayMonths, sourceRowNumber, diagnostics),
            ReadInteger(Cell(EmployeeImportSpreadsheetHeaders.CompanyChangeCount), EmployeeImportSpreadsheetHeaders.CompanyChangeCount, sourceRowNumber, diagnostics),
            ReadDecimal(Cell(EmployeeImportSpreadsheetHeaders.JobChangeRate), EmployeeImportSpreadsheetHeaders.JobChangeRate, sourceRowNumber, diagnostics),
            ReadInteger(Cell(EmployeeImportSpreadsheetHeaders.StayLabel), EmployeeImportSpreadsheetHeaders.StayLabel, sourceRowNumber, diagnostics),
            new ReadOnlyDictionary<string, string?>(rawValues),
            diagnostics.AsReadOnly());
    }

    private static decimal? ReadDecimal(
        IXLCell cell,
        string header,
        int sourceRowNumber,
        ICollection<EmployeeImportDiagnostic> diagnostics)
    {
        var value = cell.Value;
        if (value.IsBlank)
            return null;
        if (value.IsNumber)
            return (decimal)value.GetNumber();

        var text = NormalizeText(GetVisibleText(cell));
        if (text is not null && TryParseDecimal(text, out var parsed))
            return parsed;

        diagnostics.Add(InvalidValue("invalid_numeric_value", header, sourceRowNumber));
        return null;
    }

    private static int? ReadInteger(
        IXLCell cell,
        string header,
        int sourceRowNumber,
        ICollection<EmployeeImportDiagnostic> diagnostics)
    {
        var decimalValue = ReadDecimalWithoutDiagnostic(cell);
        if (decimalValue is null && string.IsNullOrWhiteSpace(GetVisibleText(cell)))
            return null;
        if (decimalValue is decimal value && decimal.Truncate(value) == value
            && value >= int.MinValue && value <= int.MaxValue)
            return decimal.ToInt32(value);

        diagnostics.Add(InvalidValue("invalid_integer_value", header, sourceRowNumber));
        return null;
    }

    private static DateOnly? ReadDate(
        IXLCell cell,
        string header,
        int sourceRowNumber,
        ICollection<EmployeeImportDiagnostic> diagnostics)
    {
        var value = cell.Value;
        if (value.IsBlank)
            return null;
        if (value.IsDateTime)
            return DateOnly.FromDateTime(value.GetDateTime());
        if (value.IsNumber)
        {
            try
            {
                return DateOnly.FromDateTime(DateTime.FromOADate(value.GetNumber()));
            }
            catch (ArgumentException)
            {
                diagnostics.Add(InvalidValue("invalid_date_value", header, sourceRowNumber));
                return null;
            }
        }

        var text = NormalizeText(GetVisibleText(cell));
        if (text is not null && TryParseDate(text, out var parsed))
            return parsed;

        diagnostics.Add(InvalidValue("invalid_date_value", header, sourceRowNumber));
        return null;
    }

    private static decimal? ReadDecimalWithoutDiagnostic(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsBlank)
            return null;
        if (value.IsNumber)
            return (decimal)value.GetNumber();

        var text = NormalizeText(GetVisibleText(cell));
        return text is not null && TryParseDecimal(text, out var parsed) ? parsed : null;
    }

    private static bool TryParseDecimal(string value, out decimal parsed) =>
        decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out parsed)
        || decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            TurkishCulture, out parsed);

    private static bool TryParseDate(string value, out DateOnly parsed)
    {
        if (DateOnly.TryParseExact(value, DateFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out parsed))
            return true;

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            || DateOnly.TryParse(value, TurkishCulture, DateTimeStyles.None, out parsed);
    }

    private static EmployeeImportDiagnostic InvalidValue(
        string code,
        string header,
        int sourceRowNumber) =>
        new(code, header, $"The value for '{header}' is invalid.",
            EmployeeImportDiagnosticSeverity.Error, sourceRowNumber);

    private static string GetVisibleText(IXLCell cell) => cell.GetFormattedString();

    private static string? NormalizeText(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }

    private static string NormalizeHeader(string value) =>
        Regex.Replace(value.Normalize(NormalizationForm.FormC).Trim(), "\\s+", " ");
}
