using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.EmployeeImports;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Application.EmployeeImports.Persistence;

public sealed class EmployeeImportService(IHrDecisionSupportDbContext context, IEmployeeSpreadsheetReader reader, IEmployeeImportRowNormalizer normalizer, IEmployeeImportRowValidator validator, IEmployeeImportDryRunService dryRun, IEmployeeImportTransactionRunner transactions, TimeProvider timeProvider, IEmployeeImportRowPersistenceHook? persistenceHook = null) : IEmployeeImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    public async Task<Result<EmployeeImportResult>> ImportAsync(EmployeeImportRequest request, CancellationToken ct = default)
    {
        if (request.Content is null || string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.FeatureSchemaVersion) || string.IsNullOrWhiteSpace(request.LabelDefinitionVersion)) return Result<EmployeeImportResult>.Failure("employee_import.invalid_request", "Import request is incomplete.");
        if (request.DryRun) return await RunDryAsync(request, ct);
        var copy = await CopyAndHashAsync(request.Content, ct); await using var content = copy.Content;
        var source = await reader.ReadAsync(content, ct); if (source.IsFailure) return Result<EmployeeImportResult>.Failure(source.Error!);
        var existing = await context.EmployeeImportBatches.FirstOrDefaultAsync(x => x.FileHash == copy.Hash, ct);
        if (existing is not null) return Result<EmployeeImportResult>.Failure("employee_import.already_imported", "A batch with this file hash already exists.");
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var batch = new EmployeeImportBatch { Id = Guid.NewGuid(), FileName = request.FileName.Trim(), FileHash = copy.Hash, DatasetSplit = request.DatasetSplit, ObservationDate = request.ObservationDate, ImportedAtUtc = now, Status = EmployeeImportBatchStatus.Processing };
        context.EmployeeImportBatches.Add(batch); await context.SaveChangesAsync(ct);
        var catalog = await EnsureCatalogAsync(ct); var rows = new List<EmployeeImportRowResult>(); var counts = new Counts();
        foreach (var sourceRow in source.Value.Rows)
        {
            ct.ThrowIfCancellationRequested(); var normalized = normalizer.Normalize(sourceRow); var validation = validator.Validate(normalized, new(request.DatasetSplit, request.ObservationDate));
            var rowCounts = new Counts();
            try { var rowResult = await transactions.ExecuteAsync(token => PersistRowAsync(batch, normalized, validation, request, catalog, rowCounts, token), ct); counts.Add(rowCounts); rows.Add(rowResult); }
            catch (Exception) { rows.Add(await PersistFailureAsync(batch, normalized, validation.Diagnostics, "row_persistence_failed", ct)); }
        }
        batch.TotalRowCount = rows.Count; batch.SuccessfulRowCount = rows.Count(x => x.Status is EmployeeImportRowStatus.Succeeded or EmployeeImportRowStatus.SucceededWithWarnings); batch.FailedRowCount = rows.Count(x => x.Status == EmployeeImportRowStatus.Failed); batch.Status = batch.FailedRowCount == 0 ? EmployeeImportBatchStatus.Completed : EmployeeImportBatchStatus.CompletedWithErrors; await context.SaveChangesAsync(ct);
        return Result<EmployeeImportResult>.Success(Build(batch.Id, false, request, rows, counts));
    }
    private async Task<Result<EmployeeImportResult>> RunDryAsync(EmployeeImportRequest request, CancellationToken ct) { var result = await dryRun.DryRunAsync(request.Content, new(request.DatasetSplit, request.ObservationDate), ct); if (result.IsFailure) return Result<EmployeeImportResult>.Failure(result.Error!); var rows = result.Value.Rows.Select(x => new EmployeeImportRowResult(x.SourceRowNumber, x.EmployeeCode, x.Status == EmployeeImportValidationStatus.Invalid ? EmployeeImportRowStatus.Failed : x.Status == EmployeeImportValidationStatus.ValidWithWarnings ? EmployeeImportRowStatus.SucceededWithWarnings : EmployeeImportRowStatus.Succeeded, null, x.Diagnostics)).ToArray(); return Result<EmployeeImportResult>.Success(Build(null, true, request, rows, new())); }
    private async Task<EmployeeImportRowResult> PersistRowAsync(EmployeeImportBatch batch, EmployeeImportNormalizedRow row, EmployeeImportRowValidationResult validation, EmployeeImportRequest request, Catalog catalog, Counts counts, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime; var diagnostics = validation.Diagnostics.ToList(); var importRow = new EmployeeImportRow { Id = Guid.NewGuid(), ImportBatchId = batch.Id, SourceRowNumber = row.SourceRowNumber, ExternalEmployeeCode = row.EmployeeCode, RawPayloadJson = JsonSerializer.Serialize(row.RawValues, JsonOptions), ValidationErrorsJson = JsonSerializer.Serialize(diagnostics, JsonOptions), CreatedAtUtc = now }; context.EmployeeImportRows.Add(importRow);
        if (validation.Status == EmployeeImportValidationStatus.Invalid) { importRow.ImportStatus = EmployeeImportRowStatus.Failed; await context.SaveChangesAsync(ct); return new(row.SourceRowNumber, row.EmployeeCode, importRow.ImportStatus, null, diagnostics); }
        var employee = await context.Employees.Include(x => x.Person).FirstOrDefaultAsync(x => x.EmployeeCode == row.EmployeeCode, ct); var person = await context.People.FirstOrDefaultAsync(x => x.AnonymousCode == row.EmployeeCode, ct);
        if (employee is not null && person is not null && employee.PersonId != person.Id) throw new InvalidOperationException();
        if (employee is null) { if (person is null) { person = new Person { Id = Guid.NewGuid(), AnonymousCode = row.EmployeeCode!, CreatedAtUtc = now }; context.People.Add(person); } employee = new Employee { Id = Guid.NewGuid(), PersonId = person.Id, EmployeeCode = row.EmployeeCode!, HireDate = row.HireDate!.Value, EmploymentStatus = row.TerminationDate is null ? EmploymentStatus.Active : EmploymentStatus.Terminated }; context.Employees.Add(employee); counts.NewEmployees++; } else { if (row.HireDate is not null) employee.HireDate = row.HireDate.Value; if (row.TerminationDate is not null) employee.TerminationDate = row.TerminationDate; employee.EmploymentStatus = employee.TerminationDate is null ? EmploymentStatus.Active : EmploymentStatus.Terminated; counts.UpdatedEmployees++; person = employee.Person; }
        persistenceHook?.AfterPersonAndEmployeePrepared(row);
        if (!await context.EmployeeAssignments.AnyAsync(x => x.EmployeeId == employee.Id && x.DepartmentId == catalog.It.Id && x.PositionId == catalog.Backend.Id && x.StartDate == null && x.EndDate == null, ct)) { context.EmployeeAssignments.Add(new() { Id = Guid.NewGuid(), EmployeeId = employee.Id, DepartmentId = catalog.It.Id, PositionId = catalog.Backend.Id }); }
        for (var i = 0; i < row.PreviousPositions.Count; i++) if (!await context.PersonPriorPositionEvidences.AnyAsync(x => x.PersonId == person!.Id && x.SequenceNumber == i + 1 && x.Title == row.PreviousPositions[i], ct)) context.PersonPriorPositionEvidences.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, ImportRowId = importRow.Id, Title = row.PreviousPositions[i], SequenceNumber = i + 1, CreatedAtUtc = now });
        foreach (var item in row.Competencies) { var competency = await context.Competencies.FirstOrDefaultAsync(x => x.Code == item.Code, ct); if (competency is null) { competency = new Competency { Id = Guid.NewGuid(), Code = item.Code, Name = item.Name, CompetencyCategory = item.Category, IsActive = true }; context.Competencies.Add(competency); } if (!await context.PersonCompetencies.AnyAsync(x => x.PersonId == person!.Id && x.CompetencyId == competency.Id, ct)) { context.PersonCompetencies.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, CompetencyId = competency.Id }); counts.CompetencyLinksCreated++; } }
        foreach (var projectText in row.ProjectExperiences) { var existingProjectLink = await context.PersonProjects.Include(x => x.Project).FirstOrDefaultAsync(x => x.PersonId == person!.Id && x.Description == projectText, ct); if (existingProjectLink is null) { var project = new Project { Id = Guid.NewGuid(), Name = ProjectName(projectText), Description = projectText }; context.Projects.Add(project); context.PersonProjects.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, ProjectId = project.Id, Description = projectText }); counts.ProjectsCreated++; } }
        foreach (var item in row.SectorExperiences)
        {
            if (!catalog.SectorsByCode.TryGetValue(item.Code, out var sector))
            {
                sector = await context.Sectors.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
                if (sector is null) { sector = new Sector { Id = Guid.NewGuid(), Code = item.Code, Name = item.Name }; context.Sectors.Add(sector); }
                catalog.SectorsByCode[item.Code] = sector;
            }
            if (!string.Equals(sector.Name, item.Name, StringComparison.Ordinal)) diagnostics.Add(Warning("sector_name_conflict", "SectorExperience", "The existing sector name was preserved.", row));
            var experience = await context.PersonSectorExperiences.FirstOrDefaultAsync(x => x.PersonId == person!.Id && x.SectorId == sector.Id, ct);
            if (experience is null) { context.PersonSectorExperiences.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, SectorId = sector.Id, ExperienceMonths = item.ExperienceMonths, Notes = null }); counts.SectorLinksCreated++; }
            else if (item.ExperienceMonths is not null)
            {
                if (experience.ExperienceMonths is not null && experience.ExperienceMonths != item.ExperienceMonths) diagnostics.Add(Warning("sector_experience_conflict", "SectorExperience", "The imported sector experience value was applied.", row));
                experience.ExperienceMonths = item.ExperienceMonths;
            }
        }
        if (row.EducationLevel is not null)
        {
            var field = NormalizeEducationField(row.EducationField);
            var candidates = await context.EducationRecords.Where(x => x.PersonId == person!.Id && x.DegreeLevel == row.EducationLevel.Value && x.Institution == null && x.StartDate == null && x.GraduationDate == null).ToListAsync(ct);
            if (!candidates.Any(x => NormalizeEducationField(x.FieldOfStudy) == field)) { context.EducationRecords.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, Institution = null, FieldOfStudy = row.EducationField, DegreeLevel = row.EducationLevel.Value, StartDate = null, GraduationDate = null }); counts.EducationRecordsCreated++; }
        }
        foreach (var name in row.Certificates)
        {
            var code = CatalogCode("CERT", name);
            if (!catalog.CertificatesByCode.TryGetValue(code, out var certificate))
            {
                certificate = await context.Certificates.FirstOrDefaultAsync(x => x.Code == code, ct);
                if (certificate is null) { certificate = new Certificate { Id = Guid.NewGuid(), Code = code, Name = name, Issuer = null }; context.Certificates.Add(certificate); }
                catalog.CertificatesByCode[code] = certificate;
            }
            if (!string.Equals(certificate.Name, name, StringComparison.Ordinal)) diagnostics.Add(Warning("certificate_name_conflict", "Certificate", "The existing certificate name was preserved.", row));
            if (!await context.PersonCertificates.AnyAsync(x => x.PersonId == person!.Id && x.CertificateId == certificate.Id && x.IssueDate == null && x.ExpirationDate == null && x.CredentialCode == null, ct)) { context.PersonCertificates.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, CertificateId = certificate.Id, IssueDate = null, ExpirationDate = null, CredentialCode = null }); counts.CertificateLinksCreated++; }
        }
        foreach (var item in row.Languages)
        {
            if (!catalog.LanguagesByCode.TryGetValue(item.Code, out var language))
            {
                language = await context.Languages.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
                if (language is null) { language = new Language { Id = Guid.NewGuid(), Code = item.Code, Name = item.Name }; context.Languages.Add(language); }
                catalog.LanguagesByCode[item.Code] = language;
            }
            if (!string.Equals(language.Name, item.Name, StringComparison.Ordinal)) diagnostics.Add(Warning("language_name_conflict", "Language", "The existing language name was preserved.", row));
            var link = await context.PersonLanguages.FirstOrDefaultAsync(x => x.PersonId == person!.Id && x.LanguageId == language.Id, ct);
            if (link is null) { context.PersonLanguages.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, LanguageId = language.Id, ProficiencyLevel = item.ProficiencyLevel, IsNative = item.IsNative }); counts.LanguageLinksCreated++; }
            else
            {
                if (link.ProficiencyLevel is null && item.ProficiencyLevel is not null) link.ProficiencyLevel = item.ProficiencyLevel;
                else if (link.ProficiencyLevel is not null && item.ProficiencyLevel is not null && link.ProficiencyLevel != item.ProficiencyLevel) diagnostics.Add(Warning("language_proficiency_conflict", "Language", "The existing language proficiency was preserved.", row));
                if (item.IsNative) link.IsNative = true;
            }
        }
        foreach (var item in row.WorkModes)
        {
            if (!catalog.WorkModesByCode.TryGetValue(item.Code, out var workMode))
            {
                workMode = await context.WorkModes.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
                if (workMode is null) { workMode = new WorkMode { Id = Guid.NewGuid(), Code = item.Code, Name = item.Name }; context.WorkModes.Add(workMode); }
                catalog.WorkModesByCode[item.Code] = workMode;
            }
            if (!string.Equals(workMode.Name, item.Name, StringComparison.Ordinal)) diagnostics.Add(Warning("work_mode_name_conflict", "WorkMode", "The existing work mode name was preserved.", row));
            var experience = await context.PersonWorkModeExperiences.FirstOrDefaultAsync(x => x.PersonId == person!.Id && x.WorkModeId == workMode.Id, ct);
            if (experience is null) { context.PersonWorkModeExperiences.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, WorkModeId = workMode.Id, ExperienceMonths = item.ExperienceMonths }); counts.WorkModeLinksCreated++; }
            else if (item.ExperienceMonths is not null)
            {
                if (experience.ExperienceMonths is not null && experience.ExperienceMonths != item.ExperienceMonths) diagnostics.Add(Warning("work_mode_experience_conflict", "WorkMode", "The imported work mode experience value was applied.", row));
                experience.ExperienceMonths = item.ExperienceMonths;
            }
        }
        var snapshot = new EmployeeCareerFeatureSnapshot { Id = Guid.NewGuid(), EmployeeId = employee.Id, ImportBatchId = batch.Id, ObservedAt = request.ObservationDate, TotalExperienceMonths = row.TotalExperienceMonths, BackendExperienceMonths = row.BackendExperienceMonths, HasPreviousCompany = row.CompanyChangeCount is null ? null : row.CompanyChangeCount > 0, PreviousCompanyAverageStayMonths = row.PreviousCompanyAverageStayMonths, ShortestPreviousJobMonths = row.ShortestPreviousJobMonths, LongestPreviousJobMonths = row.LongestPreviousJobMonths, LastPreviousCompanyStayMonths = row.LastPreviousCompanyStayMonths, CompanyChangeCount = row.CompanyChangeCount, ImportedJobChangeRate = row.JobChangeRate, ObservedCompanyTenureMonths = row.CompanyTenureMonths, FeatureSource = EmployeeCareerFeatureSource.ImportedAggregate, FeatureSchemaVersion = request.FeatureSchemaVersion, CreatedAtUtc = now }; context.EmployeeCareerFeatureSnapshots.Add(snapshot); counts.FeatureSnapshotsCreated++;
        if (request.DatasetSplit is EmployeeDatasetSplit.Training or EmployeeDatasetSplit.HoldoutTest) { context.EmployeeRetentionLabels.Add(new() { Id = Guid.NewGuid(), EmployeeCareerFeatureSnapshotId = snapshot.Id, Label = row.StayLabel!.Value, LabelSource = EmployeeRetentionLabelSource.ImportedDataset, LabelDefinitionVersion = request.LabelDefinitionVersion, CreatedAtUtc = now }); counts.RetentionLabelsCreated++; }
        importRow.EmployeeId = employee.Id; importRow.ValidationErrorsJson = JsonSerializer.Serialize(diagnostics, JsonOptions); importRow.ImportStatus = diagnostics.Any(x => x.Severity == EmployeeImportDiagnosticSeverity.Warning) ? EmployeeImportRowStatus.SucceededWithWarnings : EmployeeImportRowStatus.Succeeded; await context.SaveChangesAsync(ct); return new(row.SourceRowNumber, row.EmployeeCode, importRow.ImportStatus, employee.Id, diagnostics);
    }
    private async Task<EmployeeImportRowResult> PersistFailureAsync(EmployeeImportBatch batch, EmployeeImportNormalizedRow row, IReadOnlyList<EmployeeImportDiagnostic> diagnostics, string code, CancellationToken ct) { var all = diagnostics.Append(new EmployeeImportDiagnostic(code, null, "The row could not be persisted.", EmployeeImportDiagnosticSeverity.Error, row.SourceRowNumber)).ToList(); var externalCode = row.EmployeeCode; if (!string.IsNullOrWhiteSpace(externalCode) && await context.EmployeeImportRows.AnyAsync(x => x.ImportBatchId == batch.Id && x.ExternalEmployeeCode == externalCode, ct)) { externalCode = null; all.Add(new("failure_row_external_code_omitted", "EmployeeCode", "The failed row employee code was retained in the row result and raw payload.", EmployeeImportDiagnosticSeverity.Warning, row.SourceRowNumber)); } context.EmployeeImportRows.Add(new() { Id = Guid.NewGuid(), ImportBatchId = batch.Id, SourceRowNumber = row.SourceRowNumber, ExternalEmployeeCode = externalCode, RawPayloadJson = JsonSerializer.Serialize(row.RawValues, JsonOptions), ValidationErrorsJson = JsonSerializer.Serialize(all, JsonOptions), ImportStatus = EmployeeImportRowStatus.Failed, CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime }); await context.SaveChangesAsync(ct); return new(row.SourceRowNumber, row.EmployeeCode, EmployeeImportRowStatus.Failed, null, all); }
    private async Task<Catalog> EnsureCatalogAsync(CancellationToken ct) { var it = await context.Departments.FirstOrDefaultAsync(x => x.Code == "IT", ct); if (it is null) { it = new Department { Id = Guid.NewGuid(), Code = "IT", Name = "Information Technology", IsActive = true }; context.Departments.Add(it); } var backend = await context.Positions.FirstOrDefaultAsync(x => x.Code == "BACKEND_DEVELOPER", ct); if (backend is null) { backend = new Position { Id = Guid.NewGuid(), Code = "BACKEND_DEVELOPER", Name = "Backend Developer", IsActive = true }; context.Positions.Add(backend); } await context.SaveChangesAsync(ct); return new(it, backend, new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal), new(StringComparer.Ordinal)); }
    private static async Task<(MemoryStream Content, string Hash)> CopyAndHashAsync(Stream input, CancellationToken ct) { var copy = new MemoryStream(); await input.CopyToAsync(copy, ct); var hash = Convert.ToHexString(SHA256.HashData(copy.ToArray())).ToLowerInvariant(); copy.Position = 0; return (copy, hash); }
    private static string ProjectName(string text) { const int max = 250; if (text.Length <= max) return text; var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8]; return text[..(max - hash.Length - 1)] + "-" + hash; }
    private static string CatalogCode(string prefix, string name) { var code = Regex.Replace(name.Normalize(NormalizationForm.FormC).ToUpperInvariant(), "[^A-Z0-9]+", "_").Trim('_'); return code.Length == 0 ? prefix + "_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8] : code; }
    private static string? NormalizeEducationField(string? value) => string.IsNullOrWhiteSpace(value) ? null : Regex.Replace(value.Normalize(NormalizationForm.FormC).Trim(), @"\s+", " ");
    private static EmployeeImportDiagnostic Warning(string code, string property, string message, EmployeeImportNormalizedRow row) => new(code, property, message, EmployeeImportDiagnosticSeverity.Warning, row.SourceRowNumber);
    private static EmployeeImportResult Build(Guid? id, bool dry, EmployeeImportRequest r, IReadOnlyList<EmployeeImportRowResult> rows, Counts c) => new(id, dry, r.FileName, r.DatasetSplit, rows.Count, rows.Count(x => x.Status == EmployeeImportRowStatus.Succeeded), rows.Count(x => x.Status == EmployeeImportRowStatus.SucceededWithWarnings), rows.Count(x => x.Status == EmployeeImportRowStatus.Failed), c.NewEmployees, c.UpdatedEmployees, 0, c.CompetencyLinksCreated, c.ProjectsCreated, c.SectorLinksCreated, c.EducationRecordsCreated, c.CertificateLinksCreated, c.LanguageLinksCreated, c.WorkModeLinksCreated, c.FeatureSnapshotsCreated, c.RetentionLabelsCreated, rows);
    private sealed record Catalog(Department It, Position Backend, Dictionary<string, Sector> SectorsByCode, Dictionary<string, Certificate> CertificatesByCode, Dictionary<string, Language> LanguagesByCode, Dictionary<string, WorkMode> WorkModesByCode); private sealed class Counts { public int NewEmployees { get; set; } public int UpdatedEmployees { get; set; } public int CompetencyLinksCreated { get; set; } public int ProjectsCreated { get; set; } public int SectorLinksCreated { get; set; } public int EducationRecordsCreated { get; set; } public int CertificateLinksCreated { get; set; } public int LanguageLinksCreated { get; set; } public int WorkModeLinksCreated { get; set; } public int FeatureSnapshotsCreated { get; set; } public int RetentionLabelsCreated { get; set; } public void Add(Counts value) { NewEmployees += value.NewEmployees; UpdatedEmployees += value.UpdatedEmployees; CompetencyLinksCreated += value.CompetencyLinksCreated; ProjectsCreated += value.ProjectsCreated; SectorLinksCreated += value.SectorLinksCreated; EducationRecordsCreated += value.EducationRecordsCreated; CertificateLinksCreated += value.CertificateLinksCreated; LanguageLinksCreated += value.LanguageLinksCreated; WorkModeLinksCreated += value.WorkModeLinksCreated; FeatureSnapshotsCreated += value.FeatureSnapshotsCreated; RetentionLabelsCreated += value.RetentionLabelsCreated; } }
}
