using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Application.EmployeeImports.Processing;

public sealed record NormalizedCompetencyItem(string Code, string Name, CompetencyCategory Category);
public sealed record NormalizedSectorExperience(string Code, string Name, int? ExperienceMonths);
public sealed record NormalizedLanguageExperience(string Code, string Name, LanguageProficiencyLevel? ProficiencyLevel, bool IsNative);
public sealed record NormalizedWorkModeExperience(string Code, string Name, int? ExperienceMonths);

public sealed record EmployeeImportNormalizedRow(
    int SourceRowNumber, string? EmployeeCode, string? CurrentPosition,
    decimal? TotalExperienceYears, int? TotalExperienceMonths,
    decimal? BackendExperienceYears, int? BackendExperienceMonths,
    IReadOnlyList<string> PreviousPositions, IReadOnlyList<NormalizedCompetencyItem> Competencies,
    IReadOnlyList<string> ProjectExperiences, IReadOnlyList<NormalizedSectorExperience> SectorExperiences,
    DegreeLevel? EducationLevel, string? EducationField, IReadOnlyList<string> Certificates,
    IReadOnlyList<NormalizedLanguageExperience> Languages, IReadOnlyList<NormalizedWorkModeExperience> WorkModes,
    DateOnly? HireDate, DateOnly? TerminationDate, int? CompanyTenureMonths,
    decimal? PreviousCompanyAverageStayMonths, int? ShortestPreviousJobMonths,
    int? LongestPreviousJobMonths, int? LastPreviousCompanyStayMonths, int? CompanyChangeCount,
    decimal? JobChangeRate, EmployeeRetentionLabelValue? StayLabel,
    IReadOnlyDictionary<string, string?> RawValues, IReadOnlyList<EmployeeImportDiagnostic> Diagnostics);

public enum EmployeeImportValidationStatus { Valid = 1, ValidWithWarnings = 2, Invalid = 3 }
public sealed record EmployeeImportRowValidationContext(EmployeeDatasetSplit DatasetSplit, DateOnly? ObservationDate);
public sealed record EmployeeImportRowValidationResult(EmployeeImportValidationStatus Status, IReadOnlyList<EmployeeImportDiagnostic> Diagnostics);
public sealed record EmployeeImportDryRunRequest(EmployeeDatasetSplit DatasetSplit, DateOnly? ObservationDate);
public sealed record EmployeeImportDryRunRowResult(int SourceRowNumber, string? EmployeeCode, EmployeeImportValidationStatus Status, EmployeeImportNormalizedRow NormalizedRow, IReadOnlyList<EmployeeImportDiagnostic> Diagnostics);
public sealed record EmployeeImportDryRunResult(string WorksheetName, int TotalRows, int ValidRows, int ValidWithWarningsRows, int InvalidRows, int TotalDiagnostics, int ErrorCount, int WarningCount, IReadOnlyList<EmployeeImportDryRunRowResult> Rows, int CompetencyCount, int ProjectEvidenceCount, int SectorExperienceCount, int CertificateCount, int LanguageCount, int WorkModeCount);
