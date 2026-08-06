using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.EmployeeImports.Persistence;

public sealed record EmployeeImportRequest(Stream Content, string FileName, EmployeeDatasetSplit DatasetSplit, DateOnly? ObservationDate, string FeatureSchemaVersion, string LabelDefinitionVersion, bool DryRun);
public sealed record EmployeeImportRowResult(int SourceRowNumber, string? EmployeeCode, EmployeeImportRowStatus Status, Guid? EmployeeId, IReadOnlyList<EmployeeImportDiagnostic> Diagnostics);
public sealed record EmployeeImportResult(Guid? ImportBatchId, bool IsDryRun, string FileName, EmployeeDatasetSplit DatasetSplit, int TotalRows, int SucceededRows, int SucceededWithWarningsRows, int FailedRows, int NewEmployees, int UpdatedEmployees, int SkippedRows, int CompetencyLinksCreated, int ProjectsCreated, int SectorLinksCreated, int EducationRecordsCreated, int CertificateLinksCreated, int LanguageLinksCreated, int WorkModeLinksCreated, int FeatureSnapshotsCreated, int RetentionLabelsCreated, IReadOnlyList<EmployeeImportRowResult> Rows);
public interface IEmployeeImportService { Task<Result<EmployeeImportResult>> ImportAsync(EmployeeImportRequest request, CancellationToken cancellationToken = default); }
