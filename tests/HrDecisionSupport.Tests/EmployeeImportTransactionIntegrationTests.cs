using System.Text;
using System.Text.Json;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Persistence;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.EmployeeImports;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class EmployeeImportTransactionIntegrationTests(PostgreSqlIntegrationTestFixture fixture)
{
    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_RollsBackFailedRowAndContinuesWithLaterRow()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync(); await using var db = fixture.CreateDbContext();
        var result = await Service(db, [Source(2, "E1"), Source(3, "E2")], new ThrowingHook(2)).ImportAsync(Request("rollback-and-continue"));
        Assert.True(result.IsSuccess); Assert.Equal(2, result.Value.TotalRows); Assert.Equal(1, result.Value.SucceededRows); Assert.Equal(1, result.Value.FailedRows); Assert.Equal(1, result.Value.NewEmployees); Assert.Equal(1, result.Value.CompetencyLinksCreated); Assert.Equal(1, result.Value.ProjectsCreated); Assert.Equal(1, result.Value.SectorLinksCreated); Assert.Equal(1, result.Value.EducationRecordsCreated); Assert.Equal(1, result.Value.CertificateLinksCreated); Assert.Equal(1, result.Value.LanguageLinksCreated); Assert.Equal(1, result.Value.WorkModeLinksCreated); Assert.Equal(1, result.Value.FeatureSnapshotsCreated); Assert.Equal(1, result.Value.RetentionLabelsCreated);
        Assert.Equal(EmployeeImportRowStatus.Failed, result.Value.Rows.Single(x => x.SourceRowNumber == 2).Status); Assert.Equal(EmployeeImportRowStatus.Succeeded, result.Value.Rows.Single(x => x.SourceRowNumber == 3).Status);
        var batch = await db.EmployeeImportBatches.SingleAsync(); Assert.Equal(EmployeeImportBatchStatus.CompletedWithErrors, batch.Status); Assert.Equal(2, batch.TotalRowCount); Assert.Equal(1, batch.SuccessfulRowCount); Assert.Equal(1, batch.FailedRowCount);
        Assert.Single(await db.People.ToListAsync()); Assert.Single(await db.Employees.ToListAsync()); Assert.Single(await db.EmployeeAssignments.ToListAsync()); Assert.Single(await db.PersonPriorPositionEvidences.ToListAsync()); Assert.Single(await db.Competencies.ToListAsync()); Assert.Single(await db.PersonCompetencies.ToListAsync()); Assert.Single(await db.Projects.ToListAsync()); Assert.Single(await db.PersonProjects.ToListAsync()); Assert.Single(await db.Sectors.ToListAsync()); Assert.Single(await db.PersonSectorExperiences.ToListAsync()); Assert.Single(await db.EducationRecords.ToListAsync()); Assert.Single(await db.Certificates.ToListAsync()); Assert.Single(await db.PersonCertificates.ToListAsync()); Assert.Single(await db.Languages.ToListAsync()); Assert.Single(await db.PersonLanguages.ToListAsync()); Assert.Single(await db.WorkModes.ToListAsync()); Assert.Single(await db.PersonWorkModeExperiences.ToListAsync()); Assert.Single(await db.EmployeeCareerFeatureSnapshots.ToListAsync()); Assert.Single(await db.EmployeeRetentionLabels.ToListAsync());
        var failed = await db.EmployeeImportRows.SingleAsync(x => x.SourceRowNumber == 2); Assert.Equal(EmployeeImportRowStatus.Failed, failed.ImportStatus); Assert.Equal("E1", failed.ExternalEmployeeCode); using var document = JsonDocument.Parse(failed.ValidationErrorsJson!); Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_RestoresExistingEmployeeAfterFailedRow()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync(); await using var db = fixture.CreateDbContext();
        var person = new Person { Id = Guid.NewGuid(), AnonymousCode = "E1", CreatedAtUtc = DateTime.UtcNow }; var employee = new Employee { Id = Guid.NewGuid(), PersonId = person.Id, EmployeeCode = "E1", HireDate = new DateOnly(2020, 1, 1), EmploymentStatus = EmploymentStatus.Active }; db.AddRange(person, employee); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var result = await Service(db, [Source(2, "E1", new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1))], new ThrowingHook(2)).ImportAsync(Request("existing-employee"));
        Assert.True(result.IsSuccess); Assert.Equal(0, result.Value.UpdatedEmployees); Assert.Equal(EmployeeImportRowStatus.Failed, result.Value.Rows.Single().Status);
        await using var verification = fixture.CreateDbContext(); var persisted = await verification.Employees.SingleAsync(x => x.EmployeeCode == "E1"); Assert.Equal(new DateOnly(2020, 1, 1), persisted.HireDate); Assert.Null(persisted.TerminationDate); Assert.Equal(EmploymentStatus.Active, persisted.EmploymentStatus);
    }

    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_UsesNullExternalCodeFallbackForDuplicateFailedRowAndContinues()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync(); await using var db = fixture.CreateDbContext();
        var result = await Service(db, [Source(2, "E1"), Source(3, "E1"), Source(4, "E2")]).ImportAsync(Request("duplicate-external-code"));
        Assert.True(result.IsSuccess); Assert.Equal(3, result.Value.TotalRows); Assert.Equal(2, result.Value.SucceededRows); Assert.Equal(1, result.Value.FailedRows); Assert.Equal(2, result.Value.NewEmployees); Assert.Equal(EmployeeImportBatchStatus.CompletedWithErrors, (await db.EmployeeImportBatches.SingleAsync()).Status);
        var failedResult = result.Value.Rows.Single(x => x.SourceRowNumber == 3); Assert.Equal("E1", failedResult.EmployeeCode); Assert.Equal(EmployeeImportRowStatus.Failed, failedResult.Status); Assert.Contains(failedResult.Diagnostics, x => x.Code == "failure_row_external_code_omitted");
        var failed = await db.EmployeeImportRows.SingleAsync(x => x.SourceRowNumber == 3); Assert.Null(failed.ExternalEmployeeCode); Assert.Contains("E1", failed.RawPayloadJson); Assert.Contains("failure_row_external_code_omitted", failed.ValidationErrorsJson); Assert.Equal(2, await db.Employees.CountAsync());
    }

    private static EmployeeImportService Service(HrDecisionSupportDbContext db, IReadOnlyList<EmployeeImportSourceRow> rows, IEmployeeImportRowPersistenceHook? hook = null)
    {
        var reader = new Reader(rows); var normalizer = new EmployeeImportRowNormalizer(); var validator = new EmployeeImportRowValidator(); return new(db, reader, normalizer, validator, new EmployeeImportDryRunService(reader, normalizer, validator), new EmployeeImportTransactionRunner(db), TimeProvider.System, hook);
    }

    private static EmployeeImportRequest Request(string name) => new(new MemoryStream(Encoding.UTF8.GetBytes(name)), name + ".xlsx", EmployeeDatasetSplit.Training, new DateOnly(2025, 1, 1), "v1", "l1", false);
    private static EmployeeImportSourceRow Source(int rowNumber, string employeeCode, DateOnly? hireDate = null, DateOnly? terminationDate = null) => new(rowNumber, employeeCode, "Backend", 5, 4, "Developer", "C#", null, "Project", "ERP (1 yıl)", "Lisans", "Computer Science", "AWS", "English C1", "Ofis", hireDate ?? new DateOnly(2020, 1, 1), terminationDate, 5, 12, 2, 6, 4, 1, .2m, 1, new Dictionary<string, string?> { ["EmployeeCode"] = employeeCode }, []);

    private sealed class Reader(IReadOnlyList<EmployeeImportSourceRow> rows) : IEmployeeSpreadsheetReader { public Task<Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(Stream content, CancellationToken cancellationToken = default) => Task.FromResult(Result<EmployeeImportSpreadsheetReadResult>.Success(new("Employees", [], rows, []))); }
    private sealed class ThrowingHook(int sourceRowNumber) : IEmployeeImportRowPersistenceHook { public void AfterPersonAndEmployeePrepared(EmployeeImportNormalizedRow row) { if (row.SourceRowNumber == sourceRowNumber) throw new ControlledPersistenceException(); } }
    private sealed class ControlledPersistenceException : Exception;
}
