using ClosedXML.Excel;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Infrastructure.EmployeeImports;

namespace HrDecisionSupport.Tests;

public class ClosedXmlEmployeeSpreadsheetReaderTests
{
    private const string StayLabelAlias = "Kalış etiketi (0: Kısa, 1: Normal, 2: Uzun)";

    [Fact]
    public async Task ReadAsync_ValidWorkbook_ReadsAllHeadersAndTypedRow()
    {
        using var content = CreateWorkbook(sheet =>
        {
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, " EMP-001 ");
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.CurrentPosition, " Backend Developer ");
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.TotalExperience, 5.2d);
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.BackendExperience, "4,5");
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.HireDate, new DateTime(2020, 1, 2));
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.StayLabel, 0d);
        });

        var result = await Reader.ReadAsync(content);

        Assert.True(result.IsSuccess);
        Assert.Equal("Employees", result.Value.WorksheetName);
        Assert.Equal(EmployeeImportSpreadsheetHeaders.Required, result.Value.Headers);
        var row = Assert.Single(result.Value.Rows);
        Assert.Equal(2, row.SourceRowNumber);
        Assert.Equal("EMP-001", row.AnonymousEmployeeCode);
        Assert.Equal("Backend Developer", row.CurrentPosition);
        Assert.Equal(5.2m, row.TotalExperienceYears);
        Assert.Equal(4.5m, row.BackendExperienceYears);
        Assert.Equal(new DateOnly(2020, 1, 2), row.HireDate);
        Assert.Equal(0, row.StayLabel);
        Assert.Equal(24, row.RawValues.Count);
    }

    [Fact]
    public async Task ReadAsync_ReorderedAndWhitespaceNormalizedHeaders_MapByNameWithoutChangingTurkishCharacters()
    {
        var headers = EmployeeImportSpreadsheetHeaders.Required.Reverse().ToArray();
        headers[headers.Length - 1] = "  Anonim   çalışan numarası  ";
        using var content = CreateWorkbook(sheet =>
        {
            Set(sheet, 2, headers, "  Anonim   çalışan numarası  ", "EMP-002");
            Set(sheet, 2, headers, EmployeeImportSpreadsheetHeaders.HireDate, "2021-02-03");
        }, headers);

        var result = await Reader.ReadAsync(content);

        Assert.True(result.IsSuccess);
        var row = Assert.Single(result.Value.Rows);
        Assert.Equal("EMP-002", row.AnonymousEmployeeCode);
        Assert.Equal(new DateOnly(2021, 2, 3), row.HireDate);
        Assert.DoesNotContain(result.Value.Diagnostics, diagnostic => diagnostic.Code == "unexpected_header");
    }

    [Fact]
    public async Task ReadAsync_ExactStayLabelAlias_ResolvesToCanonicalHeaderAndReadsValues()
    {
        var headers = EmployeeImportSpreadsheetHeaders.Required.ToArray(); headers[^1] = StayLabelAlias;
        using var content = CreateWorkbook(sheet =>
        {
            Set(sheet, 2, headers, StayLabelAlias, 0d); Set(sheet, 3, headers, StayLabelAlias, 1d); Set(sheet, 4, headers, StayLabelAlias, 2d);
        }, headers);

        var result = await Reader.ReadAsync(content);

        Assert.True(result.IsSuccess); Assert.Equal(EmployeeImportSpreadsheetHeaders.Required, result.Value.Headers); Assert.Equal([0, 1, 2], result.Value.Rows.Select(x => x.StayLabel)); Assert.All(result.Value.Rows, row => Assert.Contains(EmployeeImportSpreadsheetHeaders.StayLabel, row.RawValues.Keys));
    }

    [Fact]
    public async Task ReadAsync_CanonicalAndAliasStayLabelHeaders_ReturnDuplicateFailure()
    {
        var headers = EmployeeImportSpreadsheetHeaders.Required.Append(StayLabelAlias).ToArray();
        using var content = CreateWorkbook(_ => { }, headers);

        var result = await Reader.ReadAsync(content);

        Assert.True(result.IsFailure); Assert.Equal("employee_spreadsheet.duplicate_header", result.Error!.Code);
    }

    [Fact]
    public async Task ReadAsync_DuplicateStayLabelAliases_ReturnDuplicateFailure()
    {
        var headers = EmployeeImportSpreadsheetHeaders.Required.ToArray(); headers[^1] = StayLabelAlias; headers = headers.Append(StayLabelAlias).ToArray();
        using var content = CreateWorkbook(_ => { }, headers);

        var result = await Reader.ReadAsync(content);

        Assert.True(result.IsFailure); Assert.Equal("employee_spreadsheet.duplicate_header", result.Error!.Code);
    }

    [Fact]
    public async Task ReadAsync_UnknownSimilarStayLabelHeader_RemainsMissingRequiredHeader()
    {
        var headers = EmployeeImportSpreadsheetHeaders.Required.ToArray(); headers[^1] = "Kalış etiketi Kısa Normal Uzun";
        using var content = CreateWorkbook(_ => { }, headers);

        var result = await Reader.ReadAsync(content);

        Assert.True(result.IsFailure); Assert.Equal("employee_spreadsheet.missing_required_header", result.Error!.Code);
    }

    [Fact]
    public async Task ReadAsync_MissingOrDuplicateRequiredHeader_ReturnsWorkbookFailure()
    {
        var missingHeaders = EmployeeImportSpreadsheetHeaders.Required.Skip(1).ToArray();
        using var missingContent = CreateWorkbook(_ => { }, missingHeaders);
        var missing = await Reader.ReadAsync(missingContent);

        var duplicateHeaders = EmployeeImportSpreadsheetHeaders.Required.ToArray();
        duplicateHeaders[1] = duplicateHeaders[0];
        using var duplicateContent = CreateWorkbook(_ => { }, duplicateHeaders);
        var duplicate = await Reader.ReadAsync(duplicateContent);

        Assert.True(missing.IsFailure);
        Assert.Equal("employee_spreadsheet.missing_required_header", missing.Error!.Code);
        Assert.True(duplicate.IsFailure);
        Assert.Equal("employee_spreadsheet.duplicate_header", duplicate.Error!.Code);
    }

    [Fact]
    public async Task ReadAsync_ExtraHeader_ProducesWarning()
    {
        var headers = EmployeeImportSpreadsheetHeaders.Required.Append("Notlar").ToArray();
        using var content = CreateWorkbook(sheet => Set(sheet, 2, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, "EMP-003"), headers);

        var result = await Reader.ReadAsync(content);

        Assert.True(result.IsSuccess);
        var warning = Assert.Single(result.Value.Diagnostics);
        Assert.Equal("unexpected_header", warning.Code);
        Assert.Equal(EmployeeImportDiagnosticSeverity.Warning, warning.Severity);
    }

    [Fact]
    public async Task ReadAsync_EmptyWorkbook_ReturnsFailure()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Empty");
        using var content = Save(workbook);

        var result = await Reader.ReadAsync(content);

        Assert.True(result.IsFailure);
        Assert.Equal("employee_spreadsheet.empty_worksheet", result.Error!.Code);
    }

    [Fact]
    public async Task ReadAsync_SelectsFirstVisibleNonEmptyWorksheet()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Empty");
        var employees = workbook.AddWorksheet("Employees");
        WriteHeaders(employees, EmployeeImportSpreadsheetHeaders.Required);
        Set(employees, 2, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, "EMP-004");
        using var content = Save(workbook);

        var result = await Reader.ReadAsync(content);

        Assert.True(result.IsSuccess);
        Assert.Equal("Employees", result.Value.WorksheetName);
    }

    [Fact]
    public async Task ReadAsync_PreservesRawTextWithoutSplittingOrTruncation()
    {
        var projectText = new string('P', 10_000);
        using var content = CreateWorkbook(sheet =>
        {
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, "EMP-005");
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.TechnicalSkills, "C#; SQL; Docker");
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.ProjectExperiences, projectText);
        });

        var result = await Reader.ReadAsync(content);

        var row = Assert.Single(result.Value.Rows);
        Assert.Equal("C#; SQL; Docker", row.TechnicalSkillsRaw);
        Assert.Equal(projectText, row.ProjectExperiencesRaw);
        Assert.Equal(projectText, row.RawValues[EmployeeImportSpreadsheetHeaders.ProjectExperiences]);
    }

    [Theory]
    [InlineData("5,2", 5.2)]
    [InlineData("5.2", 5.2)]
    public async Task ReadAsync_StringDecimal_ParsesBothSeparators(string source, decimal expected)
    {
        using var content = CreateWorkbook(sheet =>
        {
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, "EMP-006");
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.TotalExperience, source);
        });

        var result = await Reader.ReadAsync(content);

        Assert.Equal(expected, Assert.Single(result.Value.Rows).TotalExperienceYears);
    }

    [Fact]
    public async Task ReadAsync_InvalidNumericAndFractionalInteger_ProduceDiagnosticsWithoutBlockingOtherRows()
    {
        using var content = CreateWorkbook(sheet =>
        {
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, "EMP-BAD");
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.TotalExperience, "not-a-number");
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.CompanyChangeCount, 12.5d);
            Set(sheet, 3, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, "EMP-GOOD");
            Set(sheet, 3, EmployeeImportSpreadsheetHeaders.CompanyChangeCount, 12d);
        });

        var result = await Reader.ReadAsync(content);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Rows.Count);
        var invalid = result.Value.Rows[0];
        Assert.Null(invalid.TotalExperienceYears);
        Assert.Null(invalid.CompanyChangeCount);
        Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == "invalid_numeric_value");
        Assert.Contains(invalid.Diagnostics, diagnostic => diagnostic.Code == "invalid_integer_value");
        Assert.Equal(12, result.Value.Rows[1].CompanyChangeCount);
    }

    [Fact]
    public async Task ReadAsync_ParsesDatesIncludingSerialAndFormulaAndDoesNotDiagnoseBlankTerminationDate()
    {
        using var content = CreateWorkbook(sheet =>
        {
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, "EMP-007");
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.HireDate, 45292d);
            Set(sheet, 3, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, "EMP-008");
            Set(sheet, 3, EmployeeImportSpreadsheetHeaders.HireDate, "03.02.2021");
            Set(sheet, 4, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, "EMP-009");
            var formulaCell = sheet.Cell(4, HeaderColumn(EmployeeImportSpreadsheetHeaders.HireDate));
            formulaCell.FormulaA1 = "DATE(2022,4,5)";
        });

        var result = await Reader.ReadAsync(content);

        Assert.True(result.IsSuccess);
        Assert.Equal(new DateOnly(2024, 1, 1), result.Value.Rows[0].HireDate);
        Assert.Equal(new DateOnly(2021, 2, 3), result.Value.Rows[1].HireDate);
        Assert.Equal(new DateOnly(2022, 4, 5), result.Value.Rows[2].HireDate);
        Assert.All(result.Value.Rows, row => Assert.Null(row.TerminationDate));
        Assert.DoesNotContain(result.Value.Rows.SelectMany(row => row.Diagnostics), diagnostic =>
            diagnostic.PropertyName == EmployeeImportSpreadsheetHeaders.TerminationDate);
    }

    [Fact]
    public async Task ReadAsync_InvalidDateAndEmptyCode_AreRowDiagnosticsAndBlankRowsAreIgnored()
    {
        using var content = CreateWorkbook(sheet =>
        {
            Set(sheet, 2, EmployeeImportSpreadsheetHeaders.HireDate, "not-a-date");
            Set(sheet, 4, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, "EMP-010");
            Set(sheet, 4, EmployeeImportSpreadsheetHeaders.HireDate, "2020-01-01");
        });

        var result = await Reader.ReadAsync(content);

        Assert.Equal(2, result.Value.Rows.Count);
        Assert.Equal(2, result.Value.Rows[0].SourceRowNumber);
        Assert.Contains(result.Value.Rows[0].Diagnostics, diagnostic => diagnostic.Code == "empty_employee_code");
        Assert.Contains(result.Value.Rows[0].Diagnostics, diagnostic => diagnostic.Code == "invalid_date_value");
        Assert.Equal(4, result.Value.Rows[1].SourceRowNumber);
    }

    [Fact]
    public async Task ReadAsync_UnsupportedWorkbook_ReturnsFailure_AndCancellationIsPropagated()
    {
        using var unsupported = new MemoryStream([1, 2, 3]);
        var unsupportedResult = await Reader.ReadAsync(unsupported);
        using var valid = CreateWorkbook(sheet => Set(sheet, 2, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, "EMP-011"));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.True(unsupportedResult.IsFailure);
        Assert.Equal("employee_spreadsheet.unsupported_workbook", unsupportedResult.Error!.Code);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Reader.ReadAsync(valid, cancellation.Token));
    }

    private static ClosedXmlEmployeeSpreadsheetReader Reader { get; } = new();

    private static MemoryStream CreateWorkbook(
        Action<IXLWorksheet> configure,
        IReadOnlyList<string>? headers = null)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.AddWorksheet("Employees");
        WriteHeaders(worksheet, headers ?? EmployeeImportSpreadsheetHeaders.Required);
        configure(worksheet);
        return Save(workbook);
    }

    private static MemoryStream Save(XLWorkbook workbook)
    {
        var content = new MemoryStream();
        workbook.SaveAs(content);
        content.Position = 0;
        return content;
    }

    private static void WriteHeaders(IXLWorksheet worksheet, IReadOnlyList<string> headers)
    {
        for (var index = 0; index < headers.Count; index++)
            worksheet.Cell(1, index + 1).Value = headers[index];
    }

    private static void Set(IXLWorksheet worksheet, int row, string header, object value) =>
        worksheet.Cell(row, HeaderColumn(header)).Value = XLCellValue.FromObject(value);

    private static void Set(
        IXLWorksheet worksheet,
        int row,
        IReadOnlyList<string> headers,
        string header,
        object value) =>
        worksheet.Cell(row, Array.IndexOf(headers.ToArray(), header) + 1).Value = XLCellValue.FromObject(value);

    private static int HeaderColumn(string header) =>
        Array.IndexOf(EmployeeImportSpreadsheetHeaders.Required.ToArray(), header) + 1;
}
