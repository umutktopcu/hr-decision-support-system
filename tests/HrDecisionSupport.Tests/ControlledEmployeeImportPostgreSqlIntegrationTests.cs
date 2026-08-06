using ClosedXML.Excel;
using HrDecisionSupport.EmployeeImportDryRun;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

[Collection(PostgreSqlIntegrationCollection.Name)]
[Trait("Category", "PostgreSqlIntegration")]
public sealed class ControlledEmployeeImportPostgreSqlIntegrationTests(PostgreSqlIntegrationTestFixture fixture)
{
    [PostgreSqlIntegrationFact]
    public async Task ImportAsync_SyntheticWarningWorkbook_PersistsBatchScopedVerificationAndUnknownToken()
    {
        fixture.RequireConfigured(); await fixture.ResetDatabaseAsync();
        var path = Path.Combine(Path.GetTempPath(), "hrds-controlled-import-" + Guid.NewGuid().ToString("N") + ".xlsx");
        try
        {
            CreateWorkbook(path);
            await using (var execution = new PostgreSqlEmployeeImportExecution(fixture.ConnectionString))
            {
                var command = new ControlledEmployeeImportCommand(execution, new Confirmation(), new Output());
                var exit = await command.ExecuteAsync(new(path, new DateOnly(2026, 8, 6), "1", "Development"));
                Assert.Equal(0, exit);
            }
            await using var context = fixture.CreateDbContext();
            var batch = await context.EmployeeImportBatches.SingleAsync(); var row = await context.EmployeeImportRows.SingleAsync();
            Assert.Equal(EmployeeImportBatchStatus.Completed, batch.Status); Assert.Equal(EmployeeImportRowStatus.SucceededWithWarnings, row.ImportStatus); Assert.Contains("Asenkron programlama", row.RawPayloadJson); Assert.Contains("unknown_competency", row.ValidationErrorsJson); Assert.Single(await context.PersonCompetencies.ToListAsync()); Assert.Equal(new DateOnly(2024, 6, 30), (await context.Employees.SingleAsync()).TerminationDate);
            await using var duplicateExecution = new PostgreSqlEmployeeImportExecution(fixture.ConnectionString);
            var duplicateExit = await new ControlledEmployeeImportCommand(duplicateExecution, new Confirmation(), new Output()).ExecuteAsync(new(path, new DateOnly(2026, 8, 6), "1", "Development"));
            Assert.Equal(1, duplicateExit); Assert.Single(await context.EmployeeImportBatches.ToListAsync());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }

    private static void CreateWorkbook(string path)
    {
        using var workbook = new XLWorkbook(); var sheet = workbook.AddWorksheet("Employees");
        for (var index = 0; index < EmployeeImportSpreadsheetHeaders.Required.Count; index++) sheet.Cell(1, index + 1).Value = EmployeeImportSpreadsheetHeaders.Required[index];
        Set(sheet, EmployeeImportSpreadsheetHeaders.AnonymousEmployeeCode, "SYNTHETIC-001"); Set(sheet, EmployeeImportSpreadsheetHeaders.CurrentPosition, "Backend Developer"); Set(sheet, EmployeeImportSpreadsheetHeaders.HireDate, new DateTime(2020, 1, 1)); Set(sheet, EmployeeImportSpreadsheetHeaders.TerminationDate, new DateTime(2024, 6, 30)); Set(sheet, EmployeeImportSpreadsheetHeaders.StayLabel, 1d); Set(sheet, EmployeeImportSpreadsheetHeaders.TechnicalSkills, "C#; Asenkron programlama");
        workbook.SaveAs(path);
    }
    private static void Set(IXLWorksheet sheet, string header, object value) => sheet.Cell(2, Array.IndexOf(EmployeeImportSpreadsheetHeaders.Required.ToArray(), header) + 1).Value = XLCellValue.FromObject(value);
    private sealed class Confirmation : IImportConfirmation { public Task<bool> ConfirmAsync(string requiredText, string? suppliedRowCount, CancellationToken cancellationToken) => Task.FromResult(suppliedRowCount == "1"); }
    private sealed class Output : IImportOutput { public void WriteLine(string message) { } public void WriteError(string message) { } }
}
