using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.EmployeeImports.Processing;

public sealed class EmployeeImportRowValidator : IEmployeeImportRowValidator
{
    public EmployeeImportRowValidationResult Validate(EmployeeImportNormalizedRow row, EmployeeImportRowValidationContext context)
    {
        var diagnostics=row.Diagnostics.ToList();
        void Error(string code,string property)=>diagnostics.Add(new(code,property,$"{property} is invalid.",EmployeeImportDiagnosticSeverity.Error,row.SourceRowNumber));
        void Warning(string code,string property)=>diagnostics.Add(new(code,property,$"{property} could not be fully validated.",EmployeeImportDiagnosticSeverity.Warning,row.SourceRowNumber));
        if(string.IsNullOrWhiteSpace(row.EmployeeCode))Error("employee_code_required","EmployeeCode");
        if(row.HireDate is null)Error("hire_date_required","HireDate");
        if(context.DatasetSplit is EmployeeDatasetSplit.Training or EmployeeDatasetSplit.HoldoutTest && row.StayLabel is null)Error("stay_label_required","StayLabel");
        if(row.TotalExperienceMonths<0)Error("total_experience_months_negative","TotalExperienceMonths");
        if(row.BackendExperienceMonths<0)Error("backend_experience_months_negative","BackendExperienceMonths");
        if(row.BackendExperienceMonths is not null&&row.TotalExperienceMonths is not null&&row.BackendExperienceMonths>row.TotalExperienceMonths)Error("backend_experience_exceeds_total","BackendExperienceMonths");
        if(row.TerminationDate is not null&&row.HireDate is not null&&row.TerminationDate<row.HireDate)Error("termination_before_hire","TerminationDate");
        if(row.ShortestPreviousJobMonths is not null&&row.LongestPreviousJobMonths is not null&&row.ShortestPreviousJobMonths>row.LongestPreviousJobMonths)Error("shortest_job_exceeds_longest","ShortestPreviousJobMonths");
        if(row.CompanyChangeCount<0)Error("company_change_count_negative","CompanyChangeCount");
        if(new[]{row.CompanyTenureMonths,row.ShortestPreviousJobMonths,row.LongestPreviousJobMonths,row.LastPreviousCompanyStayMonths}.Any(x=>x<0))Error("month_feature_negative","MonthFeature");
        if(row.PreviousCompanyAverageStayMonths < 0)Error("previous_company_average_stay_months_negative","PreviousCompanyAverageStayMonths");
        if(string.IsNullOrWhiteSpace(row.CurrentPosition))Warning("current_position_missing","CurrentPosition");
        if(context.ObservationDate is null&&row.TerminationDate is null)Warning("observation_date_missing","ObservationDate");
        var status=diagnostics.Any(x=>x.Severity==EmployeeImportDiagnosticSeverity.Error)?EmployeeImportValidationStatus.Invalid:diagnostics.Any()?EmployeeImportValidationStatus.ValidWithWarnings:EmployeeImportValidationStatus.Valid;
        return new(status,diagnostics.AsReadOnly());
    }
}
