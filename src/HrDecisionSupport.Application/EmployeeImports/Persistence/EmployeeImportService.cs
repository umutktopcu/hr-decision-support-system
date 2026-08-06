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

public sealed class EmployeeImportService(IHrDecisionSupportDbContext context, IEmployeeSpreadsheetReader reader, IEmployeeImportRowNormalizer normalizer, IEmployeeImportRowValidator validator, IEmployeeImportDryRunService dryRun, IEmployeeImportTransactionRunner transactions, TimeProvider timeProvider, IEmployeeImportRowPersistenceHook? persistenceHook = null, IEmployeeImportOrchestrationHook? orchestrationHook = null) : IEmployeeImportService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private const int TrackerCleanupRowInterval = 100;
    public async Task<Result<EmployeeImportResult>> ImportAsync(EmployeeImportRequest request, CancellationToken ct = default)
    {
        if (request.Content is null || string.IsNullOrWhiteSpace(request.FileName) || string.IsNullOrWhiteSpace(request.FeatureSchemaVersion) || string.IsNullOrWhiteSpace(request.LabelDefinitionVersion)) return Result<EmployeeImportResult>.Failure("employee_import.invalid_request", "Import request is incomplete.");
        if (request.DryRun) return await RunDryAsync(request, ct);
        EmployeeImportBatch? batch = null; var rows = new List<EmployeeImportRowResult>(); var counts = new Counts();
        try
        {
            var copy = await CopyAndHashAsync(request.Content, ct); await using var content = copy.Content;
            var source = await reader.ReadAsync(content, ct); if (source.IsFailure) return Result<EmployeeImportResult>.Failure(source.Error!);
            var preparedRows = source.Value.Rows.Select(sourceRow => new PreparedRow(normalizer.Normalize(sourceRow), default!)).ToArray();
            for (var i = 0; i < preparedRows.Length; i++) preparedRows[i] = preparedRows[i] with { Validation = validator.Validate(preparedRows[i].Row, new(request.DatasetSplit, request.ObservationDate)) };
            var cache = await PreloadCacheAsync(preparedRows, ct);
            var profileCache = new ProfileLinkCache();
            var existing = await context.EmployeeImportBatches.FirstOrDefaultAsync(x => x.FileHash == copy.Hash, ct);
            if (existing is not null) return Result<EmployeeImportResult>.Failure("employee_import.already_imported", "A batch with this file hash already exists.");
            var now = timeProvider.GetUtcNow().UtcDateTime;
            batch = new EmployeeImportBatch { Id = Guid.NewGuid(), FileName = request.FileName.Trim(), FileHash = copy.Hash, DatasetSplit = request.DatasetSplit, ObservationDate = request.ObservationDate, ImportedAtUtc = now, Status = EmployeeImportBatchStatus.Processing };
            context.EmployeeImportBatches.Add(batch); await context.SaveChangesAsync(ct);
            var catalog = await EnsureCatalogAsync(cache, ct); orchestrationHook?.AfterCatalogPrepared();
            foreach (var prepared in preparedRows)
            {
                ct.ThrowIfCancellationRequested(); var normalized = prepared.Row; var validation = prepared.Validation; orchestrationHook?.BeforeRow(normalized); ct.ThrowIfCancellationRequested();
                var rowCounts = new Counts();
                var mutations = new List<Action>();
                Employee? cachedEmployee = null; Person? cachedPerson = null;
                if (validation.Status != EmployeeImportValidationStatus.Invalid && !string.IsNullOrWhiteSpace(normalized.EmployeeCode))
                {
                    catalog.EmployeesByCode.TryGetValue(normalized.EmployeeCode, out cachedEmployee);
                    catalog.PeopleByCode.TryGetValue(normalized.EmployeeCode, out cachedPerson);
                    await profileCache.EnsureLoadedAsync(cachedEmployee, cachedPerson, normalized, context, ct);
                }
                try { var rowResult = await transactions.ExecuteAsync(token => PersistRowAsync(batch, normalized, validation, request, catalog, profileCache, rowCounts, mutations, token), ct); mutations.ForEach(mutation => mutation()); counts.Add(rowCounts); rows.Add(rowResult); }
                catch (OperationCanceledException) { throw; }
                catch (Exception) { rows.Add(await PersistFailureAsync(batch, normalized, validation.Diagnostics, "row_persistence_failed", ct)); }
                ObserveAndCleanupTracker(rows.Count, ct);
            }
            var status = rows.Any(x => x.Status == EmployeeImportRowStatus.Failed) ? EmployeeImportBatchStatus.CompletedWithErrors : EmployeeImportBatchStatus.Completed;
            await FinalizeBatchAsync(batch, rows, status, ct);
            return Result<EmployeeImportResult>.Success(Build(batch.Id, false, request, rows, counts));
        }
        catch (OperationCanceledException)
        {
            if (batch is not null) await TryFinalizeFailedBatchAsync(batch, rows);
            throw;
        }
        catch (Exception)
        {
            if (batch is not null) await TryFinalizeFailedBatchAsync(batch, rows);
            return Result<EmployeeImportResult>.Failure("employee_import.batch_failed", "The import could not be completed.");
        }
    }
    private void ObserveAndCleanupTracker(int completedRows, CancellationToken ct)
    {
        if (context is not DbContext db) return;
        var count = db.ChangeTracker.Entries().Count(); orchestrationHook?.AfterRowPersistence(count);
        if (completedRows % TrackerCleanupRowInterval != 0 || ct.IsCancellationRequested || db.ChangeTracker.HasChanges()) return;
        orchestrationHook?.BeforeTrackerCleanup(count);
        foreach (var entry in db.ChangeTracker.Entries().Where(entry => !RetainAcrossTrackerCleanup(entry.Entity)).ToArray()) entry.State = EntityState.Detached;
        orchestrationHook?.AfterTrackerCleanup(db.ChangeTracker.Entries().Count());
    }
    private static bool RetainAcrossTrackerCleanup(object entity) => entity is EmployeeImportBatch or Person or Employee or Department or Position or Competency or Sector or Certificate or Language or WorkMode;
    private async Task<Result<EmployeeImportResult>> RunDryAsync(EmployeeImportRequest request, CancellationToken ct) { var result = await dryRun.DryRunAsync(request.Content, new(request.DatasetSplit, request.ObservationDate), ct); if (result.IsFailure) return Result<EmployeeImportResult>.Failure(result.Error!); var rows = result.Value.Rows.Select(x => new EmployeeImportRowResult(x.SourceRowNumber, x.EmployeeCode, x.Status == EmployeeImportValidationStatus.Invalid ? EmployeeImportRowStatus.Failed : x.Status == EmployeeImportValidationStatus.ValidWithWarnings ? EmployeeImportRowStatus.SucceededWithWarnings : EmployeeImportRowStatus.Succeeded, null, x.Diagnostics)).ToArray(); return Result<EmployeeImportResult>.Success(Build(null, true, request, rows, new())); }
    private async Task<EmployeeImportRowResult> PersistRowAsync(EmployeeImportBatch batch, EmployeeImportNormalizedRow row, EmployeeImportRowValidationResult validation, EmployeeImportRequest request, Catalog catalog, ProfileLinkCache profileCache, Counts counts, ICollection<Action> mutations, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime; var diagnostics = validation.Diagnostics.ToList(); var importRow = new EmployeeImportRow { Id = Guid.NewGuid(), ImportBatchId = batch.Id, SourceRowNumber = row.SourceRowNumber, ExternalEmployeeCode = row.EmployeeCode, RawPayloadJson = JsonSerializer.Serialize(row.RawValues, JsonOptions), ValidationErrorsJson = JsonSerializer.Serialize(diagnostics, JsonOptions), CreatedAtUtc = now }; context.EmployeeImportRows.Add(importRow);
        if (validation.Status == EmployeeImportValidationStatus.Invalid) { importRow.ImportStatus = EmployeeImportRowStatus.Failed; await context.SaveChangesAsync(ct); return new(row.SourceRowNumber, row.EmployeeCode, importRow.ImportStatus, null, diagnostics); }
        catalog.EmployeesByCode.TryGetValue(row.EmployeeCode!, out var employee); catalog.PeopleByCode.TryGetValue(row.EmployeeCode!, out var person);
        if (employee is null) employee = await context.Employees.Include(x => x.Person).FirstOrDefaultAsync(x => x.EmployeeCode == row.EmployeeCode, ct); if (person is null) person = await context.People.FirstOrDefaultAsync(x => x.AnonymousCode == row.EmployeeCode, ct);
        if (employee is not null && person is not null && employee.PersonId != person.Id) throw new InvalidOperationException();
        if (employee is null) { if (person is null) { person = new Person { Id = Guid.NewGuid(), AnonymousCode = row.EmployeeCode!, CreatedAtUtc = now }; context.People.Add(person); mutations.Add(() => catalog.PeopleByCode[row.EmployeeCode!] = person); } employee = new Employee { Id = Guid.NewGuid(), PersonId = person.Id, EmployeeCode = row.EmployeeCode!, HireDate = row.HireDate!.Value, TerminationDate = row.TerminationDate, EmploymentStatus = row.TerminationDate is null ? EmploymentStatus.Active : EmploymentStatus.Terminated }; context.Employees.Add(employee); mutations.Add(() => catalog.EmployeesByCode[row.EmployeeCode!] = employee); counts.NewEmployees++; } else { if (row.HireDate is not null) employee.HireDate = row.HireDate.Value; if (row.TerminationDate is not null) employee.TerminationDate = row.TerminationDate; employee.EmploymentStatus = employee.TerminationDate is null ? EmploymentStatus.Active : EmploymentStatus.Terminated; counts.UpdatedEmployees++; person = employee.Person; }
        persistenceHook?.AfterPersonAndEmployeePrepared(row);
        var profile = profileCache.Get(person!.Id) ?? ProfileLinks.Empty(employee.Id);
        void Mutate(Action<ProfileLinks> change) => mutations.Add(() => change(profileCache.GetOrAdd(person.Id, employee.Id)));
        var assignmentKey = new AssignmentKey(employee.Id, catalog.It.Id, catalog.Backend.Id, null, null);
        if (!profile.Assignments.Contains(assignmentKey)) { context.EmployeeAssignments.Add(new() { Id = Guid.NewGuid(), EmployeeId = employee.Id, DepartmentId = catalog.It.Id, PositionId = catalog.Backend.Id }); Mutate(x => x.Assignments.Add(assignmentKey)); }
        for (var i = 0; i < row.PreviousPositions.Count; i++) { var key = new PreviousPositionKey(i + 1, row.PreviousPositions[i]); if (!profile.PreviousPositions.Contains(key)) { context.PersonPriorPositionEvidences.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, ImportRowId = importRow.Id, Title = row.PreviousPositions[i], SequenceNumber = i + 1, CreatedAtUtc = now }); Mutate(x => x.PreviousPositions.Add(key)); } }
        foreach (var item in row.Competencies) { catalog.CompetenciesByCode.TryGetValue(item.Code, out var competency); if (competency is null) { competency = await context.Competencies.FirstOrDefaultAsync(x => x.Code == item.Code, ct); if (competency is null) { competency = new Competency { Id = Guid.NewGuid(), Code = item.Code, Name = item.Name, CompetencyCategory = item.Category, IsActive = true }; context.Competencies.Add(competency); } var cached = competency; mutations.Add(() => catalog.CompetenciesByCode[item.Code] = cached); } if (!profile.CompetencyIds.Contains(competency.Id)) { context.PersonCompetencies.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, CompetencyId = competency.Id }); Mutate(x => x.CompetencyIds.Add(competency.Id)); counts.CompetencyLinksCreated++; } }
        foreach (var projectText in row.ProjectExperiences) { if (!profile.ProjectTexts.Contains(projectText)) { var project = new Project { Id = Guid.NewGuid(), Name = ProjectName(projectText), Description = projectText }; context.Projects.Add(project); context.PersonProjects.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, ProjectId = project.Id, Description = projectText }); Mutate(x => x.ProjectTexts.Add(projectText)); counts.ProjectsCreated++; } }
        foreach (var item in row.SectorExperiences)
        {
            if (!catalog.SectorsByCode.TryGetValue(item.Code, out var sector))
            {
                sector = await context.Sectors.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
                if (sector is null) { sector = new Sector { Id = Guid.NewGuid(), Code = item.Code, Name = item.Name }; context.Sectors.Add(sector); }
                var cached = sector; mutations.Add(() => catalog.SectorsByCode[item.Code] = cached);
            }
            if (!string.Equals(sector.Name, item.Name, StringComparison.Ordinal)) diagnostics.Add(Warning("sector_name_conflict", "SectorExperience", "The existing sector name was preserved.", row));
            var hasExperience = profile.Sectors.TryGetValue(sector.Id, out var experience);
            if (!hasExperience) { var entity = new PersonSectorExperience { Id = Guid.NewGuid(), PersonId = person.Id, SectorId = sector.Id, ExperienceMonths = item.ExperienceMonths, Notes = null }; context.PersonSectorExperiences.Add(entity); Mutate(x => x.Sectors[sector.Id] = new(entity.Id, entity.ExperienceMonths)); counts.SectorLinksCreated++; }
            else if (item.ExperienceMonths is not null)
            {
                if (experience.ExperienceMonths is not null && experience.ExperienceMonths != item.ExperienceMonths) diagnostics.Add(Warning("sector_experience_conflict", "SectorExperience", "The imported sector experience value was applied.", row));
                var entity = await context.PersonSectorExperiences.FindAsync([experience.Id], ct) ?? throw new InvalidOperationException(); entity.ExperienceMonths = item.ExperienceMonths; var months = item.ExperienceMonths; Mutate(x => x.Sectors[sector.Id] = experience with { ExperienceMonths = months });
            }
        }
        if (row.EducationLevel is not null)
        {
            var field = NormalizeEducationField(row.EducationField);
            var key = new EducationKey(row.EducationLevel.Value, field);
            if (!profile.Educations.Contains(key)) { context.EducationRecords.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, Institution = null, FieldOfStudy = row.EducationField, DegreeLevel = row.EducationLevel.Value, StartDate = null, GraduationDate = null }); Mutate(x => x.Educations.Add(key)); counts.EducationRecordsCreated++; }
        }
        foreach (var name in row.Certificates)
        {
            var code = CatalogCode("CERT", name);
            if (!catalog.CertificatesByCode.TryGetValue(code, out var certificate))
            {
                certificate = await context.Certificates.FirstOrDefaultAsync(x => x.Code == code, ct);
                if (certificate is null) { certificate = new Certificate { Id = Guid.NewGuid(), Code = code, Name = name, Issuer = null }; context.Certificates.Add(certificate); }
                var cached = certificate; mutations.Add(() => catalog.CertificatesByCode[code] = cached);
            }
            if (!string.Equals(certificate.Name, name, StringComparison.Ordinal)) diagnostics.Add(Warning("certificate_name_conflict", "Certificate", "The existing certificate name was preserved.", row));
            if (!profile.CertificateIds.Contains(certificate.Id)) { context.PersonCertificates.Add(new() { Id = Guid.NewGuid(), PersonId = person.Id, CertificateId = certificate.Id, IssueDate = null, ExpirationDate = null, CredentialCode = null }); Mutate(x => x.CertificateIds.Add(certificate.Id)); counts.CertificateLinksCreated++; }
        }
        foreach (var item in row.Languages)
        {
            if (!catalog.LanguagesByCode.TryGetValue(item.Code, out var language))
            {
                language = await context.Languages.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
                if (language is null) { language = new Language { Id = Guid.NewGuid(), Code = item.Code, Name = item.Name }; context.Languages.Add(language); }
                var cached = language; mutations.Add(() => catalog.LanguagesByCode[item.Code] = cached);
            }
            if (!string.Equals(language.Name, item.Name, StringComparison.Ordinal)) diagnostics.Add(Warning("language_name_conflict", "Language", "The existing language name was preserved.", row));
            var hasLink = profile.Languages.TryGetValue(language.Id, out var link);
            if (!hasLink) { var entity = new PersonLanguage { Id = Guid.NewGuid(), PersonId = person.Id, LanguageId = language.Id, ProficiencyLevel = item.ProficiencyLevel, IsNative = item.IsNative }; context.PersonLanguages.Add(entity); Mutate(x => x.Languages[language.Id] = new(entity.Id, entity.ProficiencyLevel, entity.IsNative)); counts.LanguageLinksCreated++; }
            else
            {
                var entity = await context.PersonLanguages.FindAsync([link.Id], ct) ?? throw new InvalidOperationException(); var proficiency = link.ProficiencyLevel; var native = link.IsNative || item.IsNative;
                if (proficiency is null && item.ProficiencyLevel is not null) proficiency = item.ProficiencyLevel;
                else if (proficiency is not null && item.ProficiencyLevel is not null && proficiency != item.ProficiencyLevel) diagnostics.Add(Warning("language_proficiency_conflict", "Language", "The existing language proficiency was preserved.", row));
                entity.ProficiencyLevel = proficiency; entity.IsNative = native; Mutate(x => x.Languages[language.Id] = new(link.Id, proficiency, native));
            }
        }
        foreach (var item in row.WorkModes)
        {
            if (!catalog.WorkModesByCode.TryGetValue(item.Code, out var workMode))
            {
                workMode = await context.WorkModes.FirstOrDefaultAsync(x => x.Code == item.Code, ct);
                if (workMode is null) { workMode = new WorkMode { Id = Guid.NewGuid(), Code = item.Code, Name = item.Name }; context.WorkModes.Add(workMode); }
                var cached = workMode; mutations.Add(() => catalog.WorkModesByCode[item.Code] = cached);
            }
            if (!string.Equals(workMode.Name, item.Name, StringComparison.Ordinal)) diagnostics.Add(Warning("work_mode_name_conflict", "WorkMode", "The existing work mode name was preserved.", row));
            var hasExperience = profile.WorkModes.TryGetValue(workMode.Id, out var experience);
            if (!hasExperience) { var entity = new PersonWorkModeExperience { Id = Guid.NewGuid(), PersonId = person.Id, WorkModeId = workMode.Id, ExperienceMonths = item.ExperienceMonths }; context.PersonWorkModeExperiences.Add(entity); Mutate(x => x.WorkModes[workMode.Id] = new(entity.Id, entity.ExperienceMonths)); counts.WorkModeLinksCreated++; }
            else if (item.ExperienceMonths is not null)
            {
                if (experience.ExperienceMonths is not null && experience.ExperienceMonths != item.ExperienceMonths) diagnostics.Add(Warning("work_mode_experience_conflict", "WorkMode", "The imported work mode experience value was applied.", row));
                var entity = await context.PersonWorkModeExperiences.FindAsync([experience.Id], ct) ?? throw new InvalidOperationException(); entity.ExperienceMonths = item.ExperienceMonths; var months = item.ExperienceMonths; Mutate(x => x.WorkModes[workMode.Id] = experience with { ExperienceMonths = months });
            }
        }
        var snapshot = new EmployeeCareerFeatureSnapshot { Id = Guid.NewGuid(), EmployeeId = employee.Id, ImportBatchId = batch.Id, ObservedAt = request.ObservationDate, TotalExperienceMonths = row.TotalExperienceMonths, BackendExperienceMonths = row.BackendExperienceMonths, HasPreviousCompany = row.CompanyChangeCount is null ? null : row.CompanyChangeCount > 0, PreviousCompanyAverageStayMonths = row.PreviousCompanyAverageStayMonths, ShortestPreviousJobMonths = row.ShortestPreviousJobMonths, LongestPreviousJobMonths = row.LongestPreviousJobMonths, LastPreviousCompanyStayMonths = row.LastPreviousCompanyStayMonths, CompanyChangeCount = row.CompanyChangeCount, ImportedJobChangeRate = row.JobChangeRate, ObservedCompanyTenureMonths = row.CompanyTenureMonths, FeatureSource = EmployeeCareerFeatureSource.ImportedAggregate, FeatureSchemaVersion = request.FeatureSchemaVersion, CreatedAtUtc = now }; context.EmployeeCareerFeatureSnapshots.Add(snapshot); counts.FeatureSnapshotsCreated++;
        if (request.DatasetSplit is EmployeeDatasetSplit.Training or EmployeeDatasetSplit.HoldoutTest) { context.EmployeeRetentionLabels.Add(new() { Id = Guid.NewGuid(), EmployeeCareerFeatureSnapshotId = snapshot.Id, Label = row.StayLabel!.Value, LabelSource = EmployeeRetentionLabelSource.ImportedDataset, LabelDefinitionVersion = request.LabelDefinitionVersion, CreatedAtUtc = now }); counts.RetentionLabelsCreated++; }
        importRow.EmployeeId = employee.Id; importRow.ValidationErrorsJson = JsonSerializer.Serialize(diagnostics, JsonOptions); importRow.ImportStatus = diagnostics.Any(x => x.Severity == EmployeeImportDiagnosticSeverity.Warning) ? EmployeeImportRowStatus.SucceededWithWarnings : EmployeeImportRowStatus.Succeeded; await context.SaveChangesAsync(ct); return new(row.SourceRowNumber, row.EmployeeCode, importRow.ImportStatus, employee.Id, diagnostics);
    }
    private async Task<EmployeeImportRowResult> PersistFailureAsync(EmployeeImportBatch batch, EmployeeImportNormalizedRow row, IReadOnlyList<EmployeeImportDiagnostic> diagnostics, string code, CancellationToken ct) { var all = diagnostics.Append(new EmployeeImportDiagnostic(code, null, "The row could not be persisted.", EmployeeImportDiagnosticSeverity.Error, row.SourceRowNumber)).ToList(); var externalCode = row.EmployeeCode; if (!string.IsNullOrWhiteSpace(externalCode) && await context.EmployeeImportRows.AnyAsync(x => x.ImportBatchId == batch.Id && x.ExternalEmployeeCode == externalCode, ct)) { externalCode = null; all.Add(new("failure_row_external_code_omitted", "EmployeeCode", "The failed row employee code was retained in the row result and raw payload.", EmployeeImportDiagnosticSeverity.Warning, row.SourceRowNumber)); } context.EmployeeImportRows.Add(new() { Id = Guid.NewGuid(), ImportBatchId = batch.Id, SourceRowNumber = row.SourceRowNumber, ExternalEmployeeCode = externalCode, RawPayloadJson = JsonSerializer.Serialize(row.RawValues, JsonOptions), ValidationErrorsJson = JsonSerializer.Serialize(all, JsonOptions), ImportStatus = EmployeeImportRowStatus.Failed, CreatedAtUtc = timeProvider.GetUtcNow().UtcDateTime }); await context.SaveChangesAsync(ct); return new(row.SourceRowNumber, row.EmployeeCode, EmployeeImportRowStatus.Failed, null, all); }
    private async Task FinalizeBatchAsync(EmployeeImportBatch batch, IReadOnlyList<EmployeeImportRowResult> rows, EmployeeImportBatchStatus status, CancellationToken ct) { var persisted = await context.EmployeeImportBatches.SingleAsync(x => x.Id == batch.Id, ct); persisted.TotalRowCount = rows.Count; persisted.SuccessfulRowCount = rows.Count(x => x.Status is EmployeeImportRowStatus.Succeeded or EmployeeImportRowStatus.SucceededWithWarnings); persisted.FailedRowCount = rows.Count(x => x.Status == EmployeeImportRowStatus.Failed); persisted.Status = status; await context.SaveChangesAsync(ct); }
    private async Task TryFinalizeFailedBatchAsync(EmployeeImportBatch batch, IReadOnlyList<EmployeeImportRowResult> rows) { try { await FinalizeBatchAsync(batch, rows, EmployeeImportBatchStatus.Failed, CancellationToken.None); } catch { } }
    private async Task<Catalog> EnsureCatalogAsync(Catalog catalog, CancellationToken ct) { var it = await context.Departments.FirstOrDefaultAsync(x => x.Code == "IT", ct); if (it is null) { it = new Department { Id = Guid.NewGuid(), Code = "IT", Name = "Information Technology", IsActive = true }; context.Departments.Add(it); } var backend = await context.Positions.FirstOrDefaultAsync(x => x.Code == "BACKEND_DEVELOPER", ct); if (backend is null) { backend = new Position { Id = Guid.NewGuid(), Code = "BACKEND_DEVELOPER", Name = "Backend Developer", IsActive = true }; context.Positions.Add(backend); } await context.SaveChangesAsync(ct); return catalog with { It = it, Backend = backend }; }
    private static async Task<(MemoryStream Content, string Hash)> CopyAndHashAsync(Stream input, CancellationToken ct) { var copy = new MemoryStream(); await input.CopyToAsync(copy, ct); var hash = Convert.ToHexString(SHA256.HashData(copy.ToArray())).ToLowerInvariant(); copy.Position = 0; return (copy, hash); }
    private static string ProjectName(string text) { const int max = 250; if (text.Length <= max) return text; var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..8]; return text[..(max - hash.Length - 1)] + "-" + hash; }
    private static string CatalogCode(string prefix, string name) { var code = Regex.Replace(name.Normalize(NormalizationForm.FormC).ToUpperInvariant(), "[^A-Z0-9]+", "_").Trim('_'); return code.Length == 0 ? prefix + "_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8] : code; }
    private static string? NormalizeEducationField(string? value) => string.IsNullOrWhiteSpace(value) ? null : Regex.Replace(value.Normalize(NormalizationForm.FormC).Trim(), @"\s+", " ");
    private static EmployeeImportDiagnostic Warning(string code, string property, string message, EmployeeImportNormalizedRow row) => new(code, property, message, EmployeeImportDiagnosticSeverity.Warning, row.SourceRowNumber);
    private static EmployeeImportResult Build(Guid? id, bool dry, EmployeeImportRequest r, IReadOnlyList<EmployeeImportRowResult> rows, Counts c) => new(id, dry, r.FileName, r.DatasetSplit, rows.Count, rows.Count(x => x.Status == EmployeeImportRowStatus.Succeeded), rows.Count(x => x.Status == EmployeeImportRowStatus.SucceededWithWarnings), rows.Count(x => x.Status == EmployeeImportRowStatus.Failed), c.NewEmployees, c.UpdatedEmployees, 0, c.CompetencyLinksCreated, c.ProjectsCreated, c.SectorLinksCreated, c.EducationRecordsCreated, c.CertificateLinksCreated, c.LanguageLinksCreated, c.WorkModeLinksCreated, c.FeatureSnapshotsCreated, c.RetentionLabelsCreated, rows);
    private async Task<Catalog> PreloadCacheAsync(IReadOnlyList<PreparedRow> rows, CancellationToken ct)
    {
        var employeeCodes = rows.Select(x => x.Row.EmployeeCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).Cast<string>().ToArray(); var employees = new List<Employee>(); var people = new List<Person>();
        foreach (var group in employeeCodes.Chunk(500)) { employees.AddRange(await context.Employees.Include(x => x.Person).Where(x => group.Contains(x.EmployeeCode)).ToListAsync(ct)); people.AddRange(await context.People.Where(x => group.Contains(x.AnonymousCode)).ToListAsync(ct)); }
        var competencyCodes = rows.SelectMany(x => x.Row.Competencies).Select(x => x.Code).Distinct(StringComparer.Ordinal).ToArray(); var sectorCodes = rows.SelectMany(x => x.Row.SectorExperiences).Select(x => x.Code).Distinct(StringComparer.Ordinal).ToArray(); var certificateCodes = rows.SelectMany(x => x.Row.Certificates).Select(x => CatalogCode("CERT", x)).Distinct(StringComparer.Ordinal).ToArray(); var languageCodes = rows.SelectMany(x => x.Row.Languages).Select(x => x.Code).Distinct(StringComparer.Ordinal).ToArray(); var workModeCodes = rows.SelectMany(x => x.Row.WorkModes).Select(x => x.Code).Distinct(StringComparer.Ordinal).ToArray();
        var competencies = await context.Competencies.Where(x => competencyCodes.Contains(x.Code)).ToListAsync(ct); var sectors = await context.Sectors.Where(x => sectorCodes.Contains(x.Code)).ToListAsync(ct); var certificates = await context.Certificates.Where(x => certificateCodes.Contains(x.Code)).ToListAsync(ct); var languages = await context.Languages.Where(x => languageCodes.Contains(x.Code)).ToListAsync(ct); var workModes = await context.WorkModes.Where(x => workModeCodes.Contains(x.Code)).ToListAsync(ct);
        return new(null!, null!, employees.ToDictionary(x => x.EmployeeCode, StringComparer.Ordinal), people.ToDictionary(x => x.AnonymousCode, StringComparer.Ordinal), competencies.ToDictionary(x => x.Code, StringComparer.Ordinal), sectors.ToDictionary(x => x.Code, StringComparer.Ordinal), certificates.ToDictionary(x => x.Code, StringComparer.Ordinal), languages.ToDictionary(x => x.Code, StringComparer.Ordinal), workModes.ToDictionary(x => x.Code, StringComparer.Ordinal));
    }
    private sealed class ProfileLinkCache
    {
        private readonly Dictionary<Guid, ProfileLinks> _profiles = [];
        public ProfileLinks? Get(Guid personId) => _profiles.GetValueOrDefault(personId);
        public ProfileLinks GetOrAdd(Guid personId, Guid employeeId)
        {
            if (!_profiles.TryGetValue(personId, out var profile)) _profiles[personId] = profile = ProfileLinks.Empty(employeeId);
            else if (profile.EmployeeId == Guid.Empty) profile.EmployeeId = employeeId;
            return profile;
        }
        public async Task EnsureLoadedAsync(Employee? employee, Person? anonymousPerson, EmployeeImportNormalizedRow row, IHrDecisionSupportDbContext db, CancellationToken ct)
        {
            var person = employee?.Person ?? anonymousPerson;
            if (person is null) return;
            var profile = GetOrAdd(person.Id, employee?.Id ?? Guid.Empty);
            if (employee is not null && !profile.AssignmentsLoaded)
            {
                foreach (var item in await db.EmployeeAssignments.Where(x => x.EmployeeId == employee.Id).ToListAsync(ct)) profile.Assignments.Add(new(item.EmployeeId, item.DepartmentId, item.PositionId, item.StartDate, item.EndDate));
                profile.AssignmentsLoaded = true;
            }
            if (row.PreviousPositions.Count > 0 && !profile.PreviousPositionsLoaded)
            {
                foreach (var item in await db.PersonPriorPositionEvidences.Where(x => x.PersonId == person.Id).ToListAsync(ct)) profile.PreviousPositions.Add(new(item.SequenceNumber, item.Title));
                profile.PreviousPositionsLoaded = true;
            }
            if (row.Competencies.Count > 0 && !profile.CompetenciesLoaded)
            {
                foreach (var item in await db.PersonCompetencies.Where(x => x.PersonId == person.Id).ToListAsync(ct)) profile.CompetencyIds.Add(item.CompetencyId);
                profile.CompetenciesLoaded = true;
            }
            if (row.ProjectExperiences.Count > 0 && !profile.ProjectsLoaded)
            {
                foreach (var item in await db.PersonProjects.Include(x => x.Project).Where(x => x.PersonId == person.Id).ToListAsync(ct)) if (!string.IsNullOrWhiteSpace(item.Description)) profile.ProjectTexts.Add(item.Description);
                profile.ProjectsLoaded = true;
            }
            if (row.SectorExperiences.Count > 0 && !profile.SectorsLoaded)
            {
                foreach (var item in await db.PersonSectorExperiences.Where(x => x.PersonId == person.Id).ToListAsync(ct)) profile.Sectors[item.SectorId] = new(item.Id, item.ExperienceMonths);
                profile.SectorsLoaded = true;
            }
            if (row.EducationLevel is not null && !profile.EducationsLoaded)
            {
                foreach (var item in await db.EducationRecords.Where(x => x.PersonId == person.Id && x.Institution == null && x.StartDate == null && x.GraduationDate == null).ToListAsync(ct)) profile.Educations.Add(new(item.DegreeLevel, NormalizeEducationField(item.FieldOfStudy)));
                profile.EducationsLoaded = true;
            }
            if (row.Certificates.Count > 0 && !profile.CertificatesLoaded)
            {
                foreach (var item in await db.PersonCertificates.Where(x => x.PersonId == person.Id && x.IssueDate == null && x.ExpirationDate == null && x.CredentialCode == null).ToListAsync(ct)) profile.CertificateIds.Add(item.CertificateId);
                profile.CertificatesLoaded = true;
            }
            if (row.Languages.Count > 0 && !profile.LanguagesLoaded)
            {
                foreach (var item in await db.PersonLanguages.Where(x => x.PersonId == person.Id).ToListAsync(ct)) profile.Languages[item.LanguageId] = new(item.Id, item.ProficiencyLevel, item.IsNative);
                profile.LanguagesLoaded = true;
            }
            if (row.WorkModes.Count > 0 && !profile.WorkModesLoaded)
            {
                foreach (var item in await db.PersonWorkModeExperiences.Where(x => x.PersonId == person.Id).ToListAsync(ct)) profile.WorkModes[item.WorkModeId] = new(item.Id, item.ExperienceMonths);
                profile.WorkModesLoaded = true;
            }
        }
    }
    private sealed class ProfileLinks
    {
        public Guid EmployeeId { get; set; }
        public bool AssignmentsLoaded { get; set; } public bool PreviousPositionsLoaded { get; set; } public bool CompetenciesLoaded { get; set; } public bool ProjectsLoaded { get; set; } public bool SectorsLoaded { get; set; } public bool EducationsLoaded { get; set; } public bool CertificatesLoaded { get; set; } public bool LanguagesLoaded { get; set; } public bool WorkModesLoaded { get; set; }
        public HashSet<AssignmentKey> Assignments { get; } = []; public HashSet<PreviousPositionKey> PreviousPositions { get; } = []; public HashSet<Guid> CompetencyIds { get; } = []; public HashSet<string> ProjectTexts { get; } = new(StringComparer.Ordinal); public Dictionary<Guid, SectorLinkState> Sectors { get; } = []; public HashSet<EducationKey> Educations { get; } = []; public HashSet<Guid> CertificateIds { get; } = []; public Dictionary<Guid, LanguageLinkState> Languages { get; } = []; public Dictionary<Guid, WorkModeLinkState> WorkModes { get; } = [];
        public static ProfileLinks Empty(Guid employeeId) => new() { EmployeeId = employeeId };
    }
    private readonly record struct AssignmentKey(Guid EmployeeId, Guid DepartmentId, Guid PositionId, DateOnly? StartDate, DateOnly? EndDate);
    private readonly record struct PreviousPositionKey(int SequenceNumber, string Title);
    private readonly record struct EducationKey(DegreeLevel DegreeLevel, string? FieldOfStudy);
    private readonly record struct SectorLinkState(Guid Id, int? ExperienceMonths);
    private readonly record struct LanguageLinkState(Guid Id, LanguageProficiencyLevel? ProficiencyLevel, bool IsNative);
    private readonly record struct WorkModeLinkState(Guid Id, int? ExperienceMonths);
    private sealed record PreparedRow(EmployeeImportNormalizedRow Row, EmployeeImportRowValidationResult Validation);
    private sealed record Catalog(Department It, Position Backend, Dictionary<string, Employee> EmployeesByCode, Dictionary<string, Person> PeopleByCode, Dictionary<string, Competency> CompetenciesByCode, Dictionary<string, Sector> SectorsByCode, Dictionary<string, Certificate> CertificatesByCode, Dictionary<string, Language> LanguagesByCode, Dictionary<string, WorkMode> WorkModesByCode); private sealed class Counts { public int NewEmployees { get; set; } public int UpdatedEmployees { get; set; } public int CompetencyLinksCreated { get; set; } public int ProjectsCreated { get; set; } public int SectorLinksCreated { get; set; } public int EducationRecordsCreated { get; set; } public int CertificateLinksCreated { get; set; } public int LanguageLinksCreated { get; set; } public int WorkModeLinksCreated { get; set; } public int FeatureSnapshotsCreated { get; set; } public int RetentionLabelsCreated { get; set; } public void Add(Counts value) { NewEmployees += value.NewEmployees; UpdatedEmployees += value.UpdatedEmployees; CompetencyLinksCreated += value.CompetencyLinksCreated; ProjectsCreated += value.ProjectsCreated; SectorLinksCreated += value.SectorLinksCreated; EducationRecordsCreated += value.EducationRecordsCreated; CertificateLinksCreated += value.CertificateLinksCreated; LanguageLinksCreated += value.LanguageLinksCreated; WorkModeLinksCreated += value.WorkModeLinksCreated; FeatureSnapshotsCreated += value.FeatureSnapshotsCreated; RetentionLabelsCreated += value.RetentionLabelsCreated; } }
}
