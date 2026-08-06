using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.EmployeeImports.Spreadsheet;

public interface IEmployeeSpreadsheetReader
{
    Task<Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(
        Stream content,
        CancellationToken cancellationToken = default);
}
