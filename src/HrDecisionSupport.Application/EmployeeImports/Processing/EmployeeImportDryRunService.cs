using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;

namespace HrDecisionSupport.Application.EmployeeImports.Processing;

public sealed class EmployeeImportDryRunService(IEmployeeSpreadsheetReader reader, IEmployeeImportRowNormalizer normalizer, IEmployeeImportRowValidator validator) : IEmployeeImportDryRunService
{
    public async Task<Result<EmployeeImportDryRunResult>> DryRunAsync(Stream content, EmployeeImportDryRunRequest request, CancellationToken cancellationToken = default)
    {
        var source=await reader.ReadAsync(content,cancellationToken);
        if(source.IsFailure)return Result<EmployeeImportDryRunResult>.Failure(source.Error!);
        var rows=new List<EmployeeImportDryRunRowResult>();
        foreach(var item in source.Value.Rows){cancellationToken.ThrowIfCancellationRequested();var normalized=normalizer.Normalize(item);var validation=validator.Validate(normalized,new(request.DatasetSplit,request.ObservationDate));rows.Add(new(item.SourceRowNumber,normalized.EmployeeCode,validation.Status,normalized,validation.Diagnostics));}
        var diagnostics=source.Value.Diagnostics.Concat(rows.SelectMany(x=>x.Diagnostics)).ToArray();
        return Result<EmployeeImportDryRunResult>.Success(new(source.Value.WorksheetName,rows.Count,rows.Count(x=>x.Status==EmployeeImportValidationStatus.Valid),rows.Count(x=>x.Status==EmployeeImportValidationStatus.ValidWithWarnings),rows.Count(x=>x.Status==EmployeeImportValidationStatus.Invalid),diagnostics.Length,diagnostics.Count(x=>x.Severity==EmployeeImportDiagnosticSeverity.Error),diagnostics.Count(x=>x.Severity==EmployeeImportDiagnosticSeverity.Warning),rows.AsReadOnly(),rows.Sum(x=>x.NormalizedRow.Competencies.Count),rows.Sum(x=>x.NormalizedRow.ProjectExperiences.Count),rows.Sum(x=>x.NormalizedRow.SectorExperiences.Count),rows.Sum(x=>x.NormalizedRow.Certificates.Count),rows.Sum(x=>x.NormalizedRow.Languages.Count),rows.Sum(x=>x.NormalizedRow.WorkModes.Count)));
    }
}
