using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.EmployeeImports.Persistence;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
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
    private static EmployeeImportService Service(HrDecisionSupport.Infrastructure.Persistence.HrDecisionSupportDbContext db, EmployeeImportSourceRow row) => new(db, new Reader(row), new EmployeeImportRowNormalizer(), new EmployeeImportRowValidator(), new EmployeeImportDryRunService(new Reader(row), new EmployeeImportRowNormalizer(), new EmployeeImportRowValidator()), new Runner(), TimeProvider.System);
    private static EmployeeImportRequest Request(string name = "x") => new(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(name)), name, EmployeeDatasetSplit.Training, null, "v1", "l1", false);
    private static EmployeeImportSourceRow Source(string code, string competency, string project) => new(2, code, "Backend", 1, 1, null, competency, null, project, null, null, null, null, null, null, new DateOnly(2020, 1, 1), null, 1, null, null, null, null, 0, 0, 1, new Dictionary<string, string?>(), []);
    private sealed class Reader(EmployeeImportSourceRow row) : IEmployeeSpreadsheetReader { public Task<Result<EmployeeImportSpreadsheetReadResult>> ReadAsync(Stream s, CancellationToken c = default) => Task.FromResult(Result<EmployeeImportSpreadsheetReadResult>.Success(new("S", [], [row], []))); }
    private sealed class Runner : IEmployeeImportTransactionRunner { public Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> op, CancellationToken c = default) => op(c); }
}
