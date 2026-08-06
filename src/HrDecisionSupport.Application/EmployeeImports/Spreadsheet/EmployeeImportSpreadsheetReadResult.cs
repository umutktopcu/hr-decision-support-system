namespace HrDecisionSupport.Application.EmployeeImports.Spreadsheet;

public sealed record EmployeeImportSpreadsheetReadResult(
    string WorksheetName,
    IReadOnlyList<string> Headers,
    IReadOnlyList<EmployeeImportSourceRow> Rows,
    IReadOnlyList<EmployeeImportDiagnostic> Diagnostics);
