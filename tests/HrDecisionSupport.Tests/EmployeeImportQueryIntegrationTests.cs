using System.Diagnostics;
using System.Text;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Persistence;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.EmployeeImports;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace HrDecisionSupport.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class EmployeeImportQueryIntegrationTests(PostgreSqlIntegrationTestFixture fixture, ITestOutputHelper output)
{
    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_PreviousCompanyAverageStay_RoundTripsDecimalWithoutRounding()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync(); await using var db = fixture.CreateDbContext();
        var fractional = await Service(db, [Source(2, "AVG001") with { PreviousCompanyAverageStayMonths = 29.7m }]).ImportAsync(Request("decimal-average"));
        var whole = await Service(db, [Source(3, "AVG002") with { PreviousCompanyAverageStayMonths = 29m }]).ImportAsync(Request("whole-average"));

        Assert.True(fractional.IsSuccess); Assert.True(whole.IsSuccess); Assert.True(fractional.Value.Rows.Single().Status is EmployeeImportRowStatus.Succeeded or EmployeeImportRowStatus.SucceededWithWarnings); Assert.True(whole.Value.Rows.Single().Status is EmployeeImportRowStatus.Succeeded or EmployeeImportRowStatus.SucceededWithWarnings);
        var values = await db.EmployeeCareerFeatureSnapshots.Select(snapshot => snapshot.PreviousCompanyAverageStayMonths).OrderBy(value => value).ToArrayAsync();
        Assert.Equal([29m, 29.7m], values); Assert.DoesNotContain(fractional.Value.Rows.Single().Diagnostics, diagnostic => diagnostic.Code == "invalid_integer_value");
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_TwentyNewEmployees_UsesSharedCatalogLookups()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync(); var counter = new PostgreSqlCommandCounter(); await using var db = fixture.CreateDbContext(counter);
        var rows = Enumerable.Range(1, 20).Select(i => Source(i + 1, "E" + i.ToString("000"))).ToArray();
        counter.Start(); var result = await Service(db, rows).ImportAsync(Request("twenty-shared-catalog")); counter.Stop();
        Assert.True(result.IsSuccess); Assert.Equal(20, result.Value.SucceededRows); Assert.Equal(20, await db.Employees.CountAsync()); Assert.Equal(1, await db.Competencies.CountAsync()); Assert.Equal(20, await db.PersonCompetencies.CountAsync());
        // One preload plus the catalog insert is expected; it must not become one lookup per employee.
        Assert.True(counter["competency lookup"] <= 3, $"competency commands: {counter["competency lookup"]}");
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_ManyItemsInOneRow_DoesNotIssuePerItemProfileExistenceQueries()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync(); var counter = new PostgreSqlCommandCounter(); await using var db = fixture.CreateDbContext(counter);
        var row = Source(2, "E001") with { TechnicalSkillsRaw = "C#; Java; Python; Go; Kotlin", PreviousPositionsRaw = "Developer; Senior Developer; Lead", CertificatesRaw = "AWS; Azure; GCP", ForeignLanguagesRaw = "English C1; Almanca C1", WorkModeExperienceRaw = "Ofis, hibrit ve uzaktan", SectorExperienceRaw = "ERP (12 ay)" };
        var normalized = new EmployeeImportRowNormalizer().Normalize(row);
        Assert.Empty(normalized.Diagnostics); Assert.Equal(5, normalized.Competencies.Count); Assert.Equal(3, normalized.PreviousPositions.Count); Assert.Equal(3, normalized.Certificates.Count); Assert.Equal(2, normalized.Languages.Count); Assert.Equal(3, normalized.WorkModes.Count); Assert.Single(normalized.SectorExperiences);
        counter.Start(); var result = await Service(db, [row]).ImportAsync(Request("many-items")); counter.Stop(); output.WriteLine("Measured commands: " + counter.Describe());
        var importedRow = await db.EmployeeImportRows.SingleAsync(); var rowResult = result.Value.Rows.Single();
        output.WriteLine("Row status: " + rowResult.Status);
        foreach (var diagnostic in rowResult.Diagnostics) output.WriteLine($"Diagnostic: {diagnostic.Code} | {diagnostic.PropertyName} | {diagnostic.Severity} | {diagnostic.Message}");
        output.WriteLine("ValidationErrorsJson: " + importedRow.ValidationErrorsJson);
        Assert.True(result.IsSuccess); Assert.Equal(EmployeeImportRowStatus.Succeeded, rowResult.Status); Assert.Equal(5, result.Value.CompetencyLinksCreated); Assert.Equal(1, result.Value.SectorLinksCreated); Assert.Equal(3, result.Value.CertificateLinksCreated); Assert.Equal(2, result.Value.LanguageLinksCreated); Assert.Equal(3, result.Value.WorkModeLinksCreated);
        Assert.Equal(5, await db.PersonCompetencies.CountAsync()); Assert.Equal(1, await db.PersonSectorExperiences.CountAsync()); Assert.Equal(3, await db.PersonCertificates.CountAsync()); Assert.Equal(2, await db.PersonLanguages.CountAsync()); Assert.Equal(3, await db.PersonWorkModeExperiences.CountAsync()); Assert.Equal(3, await db.PersonPriorPositionEvidences.CountAsync()); Assert.Equal(1, await db.PersonProjects.CountAsync());
        // A new person has no persisted profile to probe, so zero is valid; in all cases this must stay below the 17 profile items.
        Assert.True(counter.ProfileLookupCount < 17, $"profile lookup commands: {counter.ProfileLookupCount}; {counter.Describe()}");
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_ExistingPersonWithDifferentFileHash_KeepsImportedLinksAndCountersStable()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync(); var counter = new PostgreSqlCommandCounter(); await using var db = fixture.CreateDbContext(counter);
        Assert.True((await Service(db, [Source(2, "EXISTING001")]).ImportAsync(Request("existing-first"))).IsSuccess);
        counter.Start(); var second = await Service(db, [Source(2, "EXISTING001")]).ImportAsync(Request("existing-second")); counter.Stop();
        Assert.True(second.IsSuccess); Assert.Equal(0, second.Value.CompetencyLinksCreated); Assert.Equal(0, second.Value.ProjectsCreated); Assert.Equal(0, second.Value.SectorLinksCreated); Assert.Equal(0, second.Value.EducationRecordsCreated); Assert.Equal(0, second.Value.CertificateLinksCreated); Assert.Equal(0, second.Value.LanguageLinksCreated); Assert.Equal(0, second.Value.WorkModeLinksCreated);
        Assert.Equal(1, await db.PersonCompetencies.CountAsync()); Assert.Equal(1, await db.PersonProjects.CountAsync());
        // The cache is intentionally import-local: this asserts one bounded, person-level preload in the second import rather than item-level probes.
        Assert.True(counter["profile link lookup"] <= 12, $"profile commands: {counter["profile link lookup"]}");
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_OneHundredSyntheticRows_CompletesWithBoundedCatalogQueries()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync(); var counter = new PostgreSqlCommandCounter(); await using var db = fixture.CreateDbContext(counter);
        var rows = Enumerable.Range(1, 100).Select(i => Source(i + 1, "SMOKE" + i.ToString("000"))).ToArray(); var timer = Stopwatch.StartNew();
        counter.Start(); var result = await Service(db, rows).ImportAsync(Request("one-hundred-smoke")); counter.Stop(); timer.Stop();
        Assert.True(result.IsSuccess); Assert.Equal(100, result.Value.SucceededRows); Assert.Equal(100, await db.Employees.CountAsync()); Assert.Equal(1, await db.Competencies.CountAsync()); Assert.Equal(1, await db.Sectors.CountAsync()); Assert.Equal(1, await db.Languages.CountAsync()); Assert.Equal(1, await db.WorkModes.CountAsync()); Assert.Equal(100, await db.PersonProjects.CountAsync());
        Assert.True(counter["competency lookup"] <= 3, $"competency commands: {counter["competency lookup"]}");
        Assert.True(counter.ReaderCount + counter.NonQueryCount + counter.ScalarCount < 1500, $"measured commands: {counter.ReaderCount + counter.NonQueryCount + counter.ScalarCount}; elapsed: {timer.Elapsed}");
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_DryRun_DoesNotExecutePersistencePreloadOrCommands()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync(); var counter = new PostgreSqlCommandCounter(); await using var db = fixture.CreateDbContext(counter);
        counter.Start(); var result = await Service(db, [Source(2, "DRY001")]).ImportAsync(Request("dry-run") with { DryRun = true }); counter.Stop();
        Assert.True(result.IsSuccess); Assert.Equal(0, counter.ReaderCount + counter.NonQueryCount + counter.ScalarCount);
    }

    private static EmployeeImportService Service(HrDecisionSupportDbContext db, IReadOnlyList<EmployeeImportSourceRow> rows)
    {
        var reader = new Reader(rows); var normalizer = new EmployeeImportRowNormalizer(); var validator = new EmployeeImportRowValidator();
        return new(db, reader, normalizer, validator, new EmployeeImportDryRunService(reader, normalizer, validator), new EmployeeImportTransactionRunner(db), TimeProvider.System);
    }
    private static EmployeeImportRequest Request(string name) => new(new MemoryStream(Encoding.UTF8.GetBytes(name)), name + ".xlsx", EmployeeDatasetSplit.Training, new DateOnly(2025, 1, 1), "v1", "l1", false);
    private static EmployeeImportSourceRow Source(int rowNumber, string employeeCode) => new(rowNumber, employeeCode, "Backend", 5, 4, "Developer", "C#", null, "Shared project", "ERP (1 yıl)", "Lisans", "Computer Science", "AWS", "English C1", "Ofis", new DateOnly(2020, 1, 1), null, 5, 12, 2, 6, 4, 1, .2m, 1, new Dictionary<string, string?> { ["EmployeeCode"] = employeeCode }, []);
    private sealed class Reader(IReadOnlyList<EmployeeImportSourceRow> rows) : IEmployeeSpreadsheetReader { public Task<Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(Stream content, CancellationToken cancellationToken = default) => Task.FromResult(Result<EmployeeImportSpreadsheetReadResult>.Success(new("Employees", [], rows, []))); }
}
