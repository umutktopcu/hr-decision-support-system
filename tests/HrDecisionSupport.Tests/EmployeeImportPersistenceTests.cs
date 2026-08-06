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
    private static EmployeeImportService Service(HrDecisionSupport.Infrastructure.Persistence.HrDecisionSupportDbContext db, EmployeeImportSourceRow row) => new(db, new Reader(row), new EmployeeImportRowNormalizer(), new EmployeeImportRowValidator(), new EmployeeImportDryRunService(new Reader(row), new EmployeeImportRowNormalizer(), new EmployeeImportRowValidator()), new Runner(), TimeProvider.System);
    private static EmployeeImportRequest Request(string name = "x", bool dryRun = false, DateOnly? observationDate = null) => new(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(name)), name, EmployeeDatasetSplit.Training, observationDate, "v1", "l1", dryRun);
    private static EmployeeImportSourceRow Source(string code, string competency, string project, string? sector = null, string? educationLevel = null, string? educationField = null) => new(2, code, "Backend", 1, 1, null, competency, null, project, sector, educationLevel, educationField, null, null, null, new DateOnly(2020, 1, 1), null, 1, null, null, null, null, 0, 0, 1, new Dictionary<string, string?>(), []);
    private sealed class Reader(EmployeeImportSourceRow row) : IEmployeeSpreadsheetReader { public Task<Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(Stream s, CancellationToken c = default) => Task.FromResult(Result<EmployeeImportSpreadsheetReadResult>.Success(new("S", [], [row], []))); }
    private sealed class Runner : IEmployeeImportTransactionRunner { public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> op, CancellationToken c = default) => op(c); }
}
