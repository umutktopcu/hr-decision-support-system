namespace HrDecisionSupport.Application.EmployeeImports.Spreadsheet;

public enum EmployeeImportDiagnosticSeverity
{
    Warning = 1,
    Error = 2
}

public sealed record EmployeeImportDiagnostic(
    string Code,
    string? PropertyName,
    string Message,
    EmployeeImportDiagnosticSeverity Severity,
    int? SourceRowNumber);
