using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;

namespace HrDecisionSupport.Application.EmployeeImports.Processing;

public interface IEmployeeImportRowNormalizer
{
    EmployeeImportNormalizedRow Normalize(EmployeeImportSourceRow sourceRow);
}

public interface IEmployeeImportRowValidator
{
    EmployeeImportRowValidationResult Validate(EmployeeImportNormalizedRow row, EmployeeImportRowValidationContext context);
}

public interface IEmployeeImportDryRunService
{
    Task<Result<EmployeeImportDryRunResult>> DryRunAsync(Stream content, EmployeeImportDryRunRequest request, CancellationToken cancellationToken = default);
}
