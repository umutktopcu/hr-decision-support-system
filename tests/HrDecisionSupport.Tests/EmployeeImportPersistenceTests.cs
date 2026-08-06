using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.EmployeeImports.Persistence;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

public class EmployeeImportPersistenceTests
{
    [Fact]
    public async Task ImportAsync_CreatesCompetencyLinkAndPersonScopedProject()
    {
        await using var db = TestDatabase.CreateContext(); var result = await Service(db, Source("E1", "C#", "A project")).ImportAsync(Request());
        Assert.True(result.IsSuccess); var competency = await db.Competencies.SingleAsync(); var link = await db.PersonCompetencies.SingleAsync(); var project = await db.Projects.SingleAsync(); var projectLink = await db.PersonProjects.SingleAsync();
        Assert.Equal("C_SHARP", competency.Code); Assert.Equal("C#", competency.Name); Assert.Equal(CompetencyCategory.ProgrammingLanguage, competency.CompetencyCategory); Assert.True(competency.IsActive); Assert.Equal(competency.Id, link.CompetencyId); Assert.Null(link.ExperienceMonths); Assert.Null(link.ProficiencyLevel); Assert.Equal("A project", project.Description); Assert.Equal("A project", projectLink.Description); Assert.Null(projectLink.Role); Assert.Equal(1, result.Value.CompetencyLinksCreated); Assert.Equal(1, result.Value.ProjectsCreated);
    }
    [Fact]
    public async Task ImportAsync_ReusesCompetencyAndDoesNotDuplicateSamePersonProject()
    {
        await using var db = TestDatabase.CreateContext(); await Service(db, Source("E1", "C#", "  A   project ")).ImportAsync(Request("a")); var second = await Service(db, Source("E1", "C#", "A project")).ImportAsync(Request("b"));
        Assert.Equal(1, await db.Competencies.CountAsync()); Assert.Equal(1, await db.PersonCompetencies.CountAsync()); Assert.Equal(1, await db.Projects.CountAsync()); Assert.Equal(1, await db.PersonProjects.CountAsync()); Assert.Equal(0, second.Value.CompetencyLinksCreated); Assert.Equal(0, second.Value.ProjectsCreated);
    }
    [Fact]
    public async Task ImportAsync_UsesSeparateProjectsForDifferentPeopleAndKeepsLongText()
    {
        var text = new string('P', 300); await using var db = TestDatabase.CreateContext(); await Service(db, Source("E1", "C#", text)).ImportAsync(Request("a")); await Service(db, Source("E2", "C#", text)).ImportAsync(Request("b"));
        var projects = await db.Projects.ToListAsync(); Assert.Equal(2, projects.Count); Assert.All(projects, x => { Assert.Equal(text, x.Description); Assert.True(x.Name.Length <= 250); Assert.EndsWith("-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)))[..8], x.Name); });
    }
    [Fact]
    public async Task ImportAsync_CreatesSectorLinkAndEducationRecord()
    {
        await using var db = TestDatabase.CreateContext(); var result = await Service(db, Source("E1", "C#", "A project", "ERP (5 yıl)", "Lisans", "Computer Science")).ImportAsync(Request(observationDate: new DateOnly(2025, 1, 1)));
        var sector = await db.Sectors.SingleAsync(); var sectorLink = await db.PersonSectorExperiences.SingleAsync(); var education = await db.EducationRecords.SingleAsync();
        Assert.Equal("ERP", sector.Code); Assert.Equal("ERP", sector.Name); Assert.Equal(sector.Id, sectorLink.SectorId); Assert.Equal(60, sectorLink.ExperienceMonths); Assert.Null(sectorLink.Notes);
        Assert.Equal(DegreeLevel.Bachelor, education.DegreeLevel); Assert.Equal("Computer Science", education.FieldOfStudy); Assert.Null(education.Institution); Assert.Null(education.StartDate); Assert.Null(education.GraduationDate); Assert.Equal(1, result.Value.SectorLinksCreated); Assert.Equal(1, result.Value.EducationRecordsCreated); Assert.Equal(EmployeeImportRowStatus.Succeeded, result.Value.Rows.Single().Status); Assert.Single(await db.EmployeeCareerFeatureSnapshots.ToListAsync()); Assert.Single(await db.EmployeeRetentionLabels.ToListAsync());
    }
    [Fact]
    public async Task ImportAsync_ReusesSectorAndAppliesExperienceMergePolicy()
    {
        await using var db = TestDatabase.CreateContext(); await Service(db, Source("E1", "C#", "A", "ERP")).ImportAsync(Request("first")); var filled = await Service(db, Source("E1", "C#", "A", "ERP (5 yıl)")).ImportAsync(Request("second")); var noDuration = await Service(db, Source("E1", "C#", "A", "ERP")).ImportAsync(Request("third"));
        Assert.Equal(1, await db.Sectors.CountAsync()); Assert.Equal(1, await db.PersonSectorExperiences.CountAsync()); Assert.Equal(60, (await db.PersonSectorExperiences.SingleAsync()).ExperienceMonths); Assert.Equal(0, filled.Value.SectorLinksCreated); Assert.Equal(0, noDuration.Value.SectorLinksCreated);
        var changed = await Service(db, Source("E1", "C#", "A", "ERP (6 yıl)")).ImportAsync(Request("fourth"));
        Assert.Equal(72, (await db.PersonSectorExperiences.SingleAsync()).ExperienceMonths); Assert.Equal(0, changed.Value.SectorLinksCreated); Assert.Equal(EmployeeImportRowStatus.SucceededWithWarnings, changed.Value.Rows.Single().Status); Assert.Contains(changed.Value.Rows.Single().Diagnostics, x => x.Code == "sector_experience_conflict");
    }
    [Fact]
    public async Task ImportAsync_PreservesExistingSectorNameWhenCodeConflicts()
    {
        await using var db = TestDatabase.CreateContext(); await Service(db, Source("E1", "C#", "A", "ERP")).ImportAsync(Request("first")); var result = await Service(db, Source("E2", "C#", "A", "ERP!")).ImportAsync(Request("second"));
        Assert.Equal("ERP", (await db.Sectors.SingleAsync()).Name); Assert.Equal(EmployeeImportRowStatus.SucceededWithWarnings, result.Value.Rows.Single().Status); Assert.Contains(result.Value.Rows.Single().Diagnostics, x => x.Code == "sector_name_conflict"); var row = await db.EmployeeImportRows.SingleAsync(x => x.ExternalEmployeeCode == "E2"); Assert.Contains("sector_name_conflict", row.ValidationErrorsJson);
    }
    [Fact]
    public async Task ImportAsync_ReusesImportedEducationWithNormalizedOrNullField()
    {
        await using var db = TestDatabase.CreateContext(); await Service(db, Source("E1", "C#", "A", null, "Lisans", " Computer   Science ")).ImportAsync(Request("first")); var second = await Service(db, Source("E1", "C#", "A", null, "Lisans", "Computer Science")).ImportAsync(Request("second")); await Service(db, Source("E1", "C#", "A", null, "Lisans", null)).ImportAsync(Request("third")); var fourth = await Service(db, Source("E1", "C#", "A", null, "Lisans", null)).ImportAsync(Request("fourth"));
        Assert.Equal(2, await db.EducationRecords.CountAsync()); Assert.Equal(0, second.Value.EducationRecordsCreated); Assert.Equal(0, fourth.Value.EducationRecordsCreated);
    }
    [Fact]
    public async Task ImportAsync_PreservesInstitutionBackedEducationAndSkipsMissingLevel()
    {
        await using var db = TestDatabase.CreateContext(); await Service(db, Source("E1", "C#", "A")).ImportAsync(Request("first")); var person = await db.People.SingleAsync(); db.EducationRecords.Add(new EducationRecord { Id = Guid.NewGuid(), PersonId = person.Id, Institution = "University", FieldOfStudy = "Computer Science", DegreeLevel = DegreeLevel.Bachelor }); await db.SaveChangesAsync(); var result = await Service(db, Source("E1", "C#", "A", null, "Lisans", "Computer Science")).ImportAsync(Request("second")); await Service(db, Source("E2", "C#", "A", null, null, "Computer Science")).ImportAsync(Request("third"));
        Assert.Equal(2, await db.EducationRecords.CountAsync()); Assert.Contains(await db.EducationRecords.ToListAsync(), x => x.Institution == "University"); Assert.Equal(1, result.Value.EducationRecordsCreated); Assert.Empty(await db.EducationRecords.Where(x => x.PersonId != person.Id).ToListAsync());
    }
    [Fact]
    public async Task ImportAsync_DryRunDoesNotPersistSectorOrEducation()
    {
        await using var db = TestDatabase.CreateContext(); var result = await Service(db, Source("E1", "C#", "A", "ERP (5 yıl)", "Lisans", "Computer Science")).ImportAsync(Request("dry", true));
        Assert.True(result.IsSuccess); Assert.Empty(db.Sectors); Assert.Empty(db.PersonSectorExperiences); Assert.Empty(db.EducationRecords); Assert.Empty(db.EmployeeImportBatches);
    }
    [Fact]
    public async Task ImportAsync_CreatesAndReusesImportedCertificateWithoutChangingRealEvidence()
    {
        await using var db = TestDatabase.CreateContext(); var first = await Service(db, Source("E1", "C#", "A", certificates: "AWS Certified")).ImportAsync(Request("first")); var certificate = await db.Certificates.SingleAsync(); var imported = await db.PersonCertificates.SingleAsync();
        Assert.Equal("AWS_CERTIFIED", certificate.Code); Assert.Equal("AWS Certified", certificate.Name); Assert.Null(certificate.Issuer); Assert.Null(imported.IssueDate); Assert.Null(imported.ExpirationDate); Assert.Null(imported.CredentialCode); Assert.Equal(1, first.Value.CertificateLinksCreated);
        var second = await Service(db, Source("E1", "C#", "A", certificates: "AWS Certified")).ImportAsync(Request("second")); Assert.Equal(1, await db.PersonCertificates.CountAsync()); Assert.Equal(0, second.Value.CertificateLinksCreated);
        db.PersonCertificates.Add(new PersonCertificate { Id = Guid.NewGuid(), PersonId = imported.PersonId, CertificateId = certificate.Id, IssueDate = new DateOnly(2020, 1, 1), CredentialCode = "REAL" }); await db.SaveChangesAsync(); await Service(db, Source("E1", "C#", "A", certificates: "AWS Certified")).ImportAsync(Request("third"));
        Assert.Equal(2, await db.PersonCertificates.CountAsync()); Assert.Contains(await db.PersonCertificates.ToListAsync(), x => x.CredentialCode == "REAL");
    }
    [Fact]
    public async Task ImportAsync_PreservesCertificateNameWhenCanonicalCodeConflicts()
    {
        await using var db = TestDatabase.CreateContext(); await Service(db, Source("E1", "C#", "A", certificates: "AWS")).ImportAsync(Request("first")); var result = await Service(db, Source("E2", "C#", "A", certificates: "AWS!")).ImportAsync(Request("second"));
        Assert.Equal("AWS", (await db.Certificates.SingleAsync()).Name); Assert.Equal(EmployeeImportRowStatus.SucceededWithWarnings, result.Value.Rows.Single().Status); Assert.Contains(result.Value.Rows.Single().Diagnostics, x => x.Code == "certificate_name_conflict");
    }
    [Fact]
    public async Task ImportAsync_MergesLanguageWithoutOverwritingExistingProficiencyOrNative()
    {
        await using var db = TestDatabase.CreateContext(); await Service(db, Source("E1", "C#", "A", languages: "English (Native)")).ImportAsync(Request("first")); var filled = await Service(db, Source("E1", "C#", "A", languages: "English C1")).ImportAsync(Request("second")); var conflict = await Service(db, Source("E1", "C#", "A", languages: "English C2")).ImportAsync(Request("third"));
        var language = await db.Languages.SingleAsync(); var link = await db.PersonLanguages.SingleAsync(); Assert.Equal("EN", language.Code); Assert.Equal("English", language.Name); Assert.Equal(LanguageProficiencyLevel.C1, link.ProficiencyLevel); Assert.True(link.IsNative); Assert.Equal(0, filled.Value.LanguageLinksCreated); Assert.Equal(0, conflict.Value.LanguageLinksCreated); Assert.Equal(EmployeeImportRowStatus.SucceededWithWarnings, conflict.Value.Rows.Single().Status); Assert.Contains(conflict.Value.Rows.Single().Diagnostics, x => x.Code == "language_proficiency_conflict");
    }
    [Fact]
    public async Task ImportAsync_PreservesLanguageNameWhenCodeConflicts()
    {
        await using var db = TestDatabase.CreateContext(); db.Languages.Add(new Language { Id = Guid.NewGuid(), Code = "EN", Name = "English language" }); await db.SaveChangesAsync(); var result = await Service(db, Source("E1", "C#", "A", languages: "English C1")).ImportAsync(Request("first"));
        Assert.Equal("English language", (await db.Languages.SingleAsync()).Name); Assert.Equal(EmployeeImportRowStatus.SucceededWithWarnings, result.Value.Rows.Single().Status); Assert.Contains(result.Value.Rows.Single().Diagnostics, x => x.Code == "language_name_conflict");
    }
    [Fact]
    public async Task ImportAsync_CreatesCanonicalWorkModesAndMergesExperience()
    {
        await using var db = TestDatabase.CreateContext(); var allModes = await Service(db, Source("E1", "C#", "A", workModes: "Ofis, hibrit ve uzaktan")).ImportAsync(Request("first"));
        Assert.Equal(3, await db.WorkModes.CountAsync()); Assert.Equal(3, await db.PersonWorkModeExperiences.CountAsync()); Assert.Equal(3, allModes.Value.WorkModeLinksCreated); Assert.Contains(await db.WorkModes.ToListAsync(), x => x.Code == "ON_SITE" && x.Name == "On-site"); Assert.Contains(await db.WorkModes.ToListAsync(), x => x.Code == "HYBRID" && x.Name == "Hybrid"); Assert.Contains(await db.WorkModes.ToListAsync(), x => x.Code == "REMOTE" && x.Name == "Remote");
        var filled = await Service(db, Source("E1", "C#", "A"), new WorkModeNormalizer(12)).ImportAsync(Request("second")); var unchanged = await Service(db, Source("E1", "C#", "A"), new WorkModeNormalizer(null)).ImportAsync(Request("third")); var conflict = await Service(db, Source("E1", "C#", "A"), new WorkModeNormalizer(24)).ImportAsync(Request("fourth"));
        var hybrid = await db.PersonWorkModeExperiences.Include(x => x.WorkMode).SingleAsync(x => x.WorkMode.Code == "HYBRID"); Assert.Equal(24, hybrid.ExperienceMonths); Assert.Equal(0, filled.Value.WorkModeLinksCreated); Assert.Equal(0, unchanged.Value.WorkModeLinksCreated); Assert.Equal(0, conflict.Value.WorkModeLinksCreated); Assert.Contains(conflict.Value.Rows.Single().Diagnostics, x => x.Code == "work_mode_experience_conflict");
    }
    [Fact]
    public async Task ImportAsync_PreservesWorkModeNameWhenCodeConflicts()
    {
        await using var db = TestDatabase.CreateContext(); db.WorkModes.Add(new WorkMode { Id = Guid.NewGuid(), Code = "REMOTE", Name = "Remote work" }); await db.SaveChangesAsync(); var result = await Service(db, Source("E1", "C#", "A", workModes: "Uzaktan")).ImportAsync(Request("first"));
        Assert.Equal("Remote work", (await db.WorkModes.SingleAsync()).Name); Assert.Equal(EmployeeImportRowStatus.SucceededWithWarnings, result.Value.Rows.Single().Status); Assert.Contains(result.Value.Rows.Single().Diagnostics, x => x.Code == "work_mode_name_conflict");
    }
    [Fact]
    public async Task ImportAsync_DryRunDoesNotPersistCertificateLanguageOrWorkMode()
    {
        await using var db = TestDatabase.CreateContext(); var result = await Service(db, Source("E1", "C#", "A", certificates: "AWS", languages: "English C1", workModes: "Ofis, hibrit ve uzaktan")).ImportAsync(Request("dry-assets", true));
        Assert.True(result.IsSuccess); Assert.Empty(db.Certificates); Assert.Empty(db.PersonCertificates); Assert.Empty(db.Languages); Assert.Empty(db.PersonLanguages); Assert.Empty(db.WorkModes); Assert.Empty(db.PersonWorkModeExperiences); Assert.Empty(db.EmployeeImportBatches);
    }
    [Fact]
    public async Task ImportAsync_PersistsDecimalPreviousCompanyAverageStayWithoutRounding()
    {
        await using var db = TestDatabase.CreateContext();
        var fractional = await Service(db, Source("E1", "C#", "A") with { PreviousCompanyAverageStayMonths = 29.7m }).ImportAsync(Request("fractional"));
        var whole = await Service(db, Source("E2", "C#", "A") with { PreviousCompanyAverageStayMonths = 29m }).ImportAsync(Request("whole"));

        Assert.True(fractional.IsSuccess); Assert.True(whole.IsSuccess); Assert.True(fractional.Value.Rows.Single().Status is EmployeeImportRowStatus.Succeeded or EmployeeImportRowStatus.SucceededWithWarnings); Assert.True(whole.Value.Rows.Single().Status is EmployeeImportRowStatus.Succeeded or EmployeeImportRowStatus.SucceededWithWarnings);
        var values = await db.EmployeeCareerFeatureSnapshots.OrderBy(snapshot => snapshot.EmployeeId).Select(snapshot => snapshot.PreviousCompanyAverageStayMonths).ToArrayAsync();
        Assert.Contains(29.7m, values); Assert.Contains(29m, values); Assert.DoesNotContain(fractional.Value.Rows.Single().Diagnostics, diagnostic => diagnostic.Code == "invalid_integer_value");
    }
    [Fact]
    public async Task ImportAsync_PersistsUnknownCompetencyRawTokenAndDiagnosticWhileLinkingKnownCompetency()
    {
        const string unknown = "Asenkron programlama";
        var source = Source("E1", "C#; " + unknown, "A") with
        {
            RawValues = new Dictionary<string, string?>
            {
                [EmployeeImportSpreadsheetHeaders.TechnicalSkills] = "C#; " + unknown
            }
        };
        await using var db = TestDatabase.CreateContext();
        var first = await Service(db, source).ImportAsync(Request("unknown-competency"));

        Assert.True(first.IsSuccess); Assert.Equal(EmployeeImportRowStatus.SucceededWithWarnings, first.Value.Rows.Single().Status); Assert.Equal(1, first.Value.SucceededWithWarningsRows); Assert.Equal(0, first.Value.FailedRows); Assert.Equal(1, first.Value.CompetencyLinksCreated);
        Assert.Single(await db.Competencies.Where(competency => competency.Code == "C_SHARP").ToListAsync()); Assert.Single(await db.PersonCompetencies.ToListAsync()); Assert.DoesNotContain(await db.Competencies.ToListAsync(), competency => competency.Name == unknown);
        var persisted = await db.EmployeeImportRows.SingleAsync(); Assert.Equal(EmployeeImportRowStatus.SucceededWithWarnings, persisted.ImportStatus); Assert.Contains(unknown, persisted.RawPayloadJson); Assert.Contains("unknown_competency", persisted.ValidationErrorsJson); Assert.Contains(unknown, persisted.ValidationErrorsJson);
        var batch = await db.EmployeeImportBatches.SingleAsync(); Assert.Equal(EmployeeImportBatchStatus.Completed, batch.Status); Assert.Equal(1, batch.SuccessfulRowCount); Assert.Equal(0, batch.FailedRowCount);

        var repeated = await Service(db, source).ImportAsync(Request("unknown-competency"));
        Assert.True(repeated.IsFailure); Assert.Equal("employee_import.already_imported", repeated.Error!.Code); Assert.Single(await db.EmployeeImportRows.ToListAsync());
    }
    private static EmployeeImportService Service(HrDecisionSupport.Infrastructure.Persistence.HrDecisionSupportDbContext db, EmployeeImportSourceRow row, IEmployeeImportRowNormalizer? normalizer = null)
    {
        normalizer ??= new EmployeeImportRowNormalizer(); return new(db, new Reader(row), normalizer, new EmployeeImportRowValidator(), new EmployeeImportDryRunService(new Reader(row), new EmployeeImportRowNormalizer(), new EmployeeImportRowValidator()), new Runner(), TimeProvider.System);
    }
    private static EmployeeImportRequest Request(string name = "x", bool dryRun = false, DateOnly? observationDate = null) => new(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(name)), name, EmployeeDatasetSplit.Training, observationDate, "v1", "l1", dryRun);
    private static EmployeeImportSourceRow Source(string code, string competency, string project, string? sector = null, string? educationLevel = null, string? educationField = null, string? certificates = null, string? languages = null, string? workModes = null) => new(2, code, "Backend", 1, 1, null, competency, null, project, sector, educationLevel, educationField, certificates, languages, workModes, new DateOnly(2020, 1, 1), null, 1, null, null, null, null, 0, 0, 1, new Dictionary<string, string?>(), []);
    private sealed class Reader(EmployeeImportSourceRow row) : IEmployeeSpreadsheetReader { public Task<Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(Stream s, CancellationToken c = default) => Task.FromResult(Result<EmployeeImportSpreadsheetReadResult>.Success(new("S", [], [row], []))); }
    private sealed class WorkModeNormalizer(int? experienceMonths) : IEmployeeImportRowNormalizer { public EmployeeImportNormalizedRow Normalize(EmployeeImportSourceRow row) => new EmployeeImportRowNormalizer().Normalize(row) with { WorkModes = [new("HYBRID", "Hybrid", experienceMonths)] }; }
    private sealed class Runner : IEmployeeImportTransactionRunner { public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> op, CancellationToken c = default) => op(c); }
}
