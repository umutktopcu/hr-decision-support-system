using System.Text.Json;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.EmployeeImportDryRun;

namespace HrDecisionSupport.Tests;

public sealed class CompetencyBackfillTests
{
    private static readonly Guid BatchId = Guid.NewGuid();
    [Fact]
    public void Planner_UsesNormalizerAndDeduplicatesPersonCanonicalCandidates()
    {
        var person = Guid.NewGuid(); var preview = Planner().Plan(BatchId, "Completed", [Row(person, "AWS", ""), Row(person, "AWS", "")], new HashSet<string>(StringComparer.Ordinal), new HashSet<(Guid, string)>(), "safe-host", "safe-db");
        var candidate = Assert.Single(preview.Candidates); Assert.Equal("AWS", candidate.Code); Assert.Equal("AWS", candidate.Name); Assert.Equal(1, preview.CandidateLinkCount); Assert.Equal(1, preview.DuplicateCandidateCount); Assert.Equal(0, preview.UnresolvedTokenCount);
    }
    [Fact]
    public void Planner_DifferentRawTokensForSameCanonicalCodeProduceOneLink()
    {
        var person = Guid.NewGuid(); var preview = Planner().Plan(BatchId, "Completed", [Row(person, "C#; C Sharp", "")], new HashSet<string>(StringComparer.Ordinal), new HashSet<(Guid, string)>(), "h", "d");
        Assert.Single(preview.Candidates); Assert.Equal("C_SHARP", preview.Candidates.Single().Code);
    }
    [Fact]
    public void Planner_ReportsUnresolvedTokensAndExistingLinks()
    {
        var person = Guid.NewGuid(); var preview = Planner().Plan(BatchId, "Completed", [Row(person, "AWS; Unknown", "")], new HashSet<string>(StringComparer.Ordinal) { "AWS" }, new HashSet<(Guid, string)> { (person, "AWS") }, "h", "d");
        Assert.Equal(1, preview.UnresolvedTokenCount); Assert.Equal(1, preview.ExistingLinkCount); Assert.Equal(0, preview.ExpectedNewLinkCount); Assert.Equal(0, preview.ExpectedNewCompetencyCount);
    }
    [Fact]
    public async Task Command_MissingBatchAndProductionAreRejectedBeforePreview()
    {
        var execution = new FakeExecution(); Assert.Equal(1, await Command(execution).ExecuteAsync(new(null, null, null, "Development"))); Assert.Equal(1, await Command(execution).ExecuteAsync(new(BatchId, null, null, "Production"))); Assert.Equal(0, execution.PreviewCalls);
    }
    [Fact]
    public async Task Command_UnresolvedAndWrongConfirmationDoNotBackfill()
    {
        var unresolved = new FakeExecution { Preview = Result<CompetencyBackfillPreview>.Success(Preview() with { UnresolvedTokenCount = 1, UnresolvedTokens = ["Unknown"] }) }; Assert.Equal(1, await Command(unresolved).ExecuteAsync(new(BatchId, null, null, "Test"))); Assert.Equal(0, unresolved.BackfillCalls);
        var wrong = new FakeExecution(); Assert.Equal(1, await new CompetencyBackfillCommand(wrong, new Confirmation(false), new Output()).ExecuteAsync(new(BatchId, "1", "1", "Test"))); Assert.Equal(0, wrong.BackfillCalls);
    }
    [Fact]
    public async Task Command_CorrectConfirmationRunsAndSecondPreviewIsIdempotent()
    {
        var execution = new FakeExecution(); var exit = await Command(execution).ExecuteAsync(new(BatchId, "1", "1", "Integration")); Assert.Equal(0, exit); Assert.Equal(1, execution.BackfillCalls); Assert.Equal((1, 1), execution.Counts); Assert.Equal(2, execution.PreviewCalls);
    }
    private static CompetencyBackfillPlanner Planner() => new(new EmployeeImportRowNormalizer());
    private static CompetencyBackfillRawRow Row(Guid person, string skills, string tools) => new(Guid.NewGuid(), person, JsonSerializer.Serialize(new Dictionary<string, string?> { [EmployeeImportSpreadsheetHeaders.TechnicalSkills] = skills, [EmployeeImportSpreadsheetHeaders.TechnologiesAndTools] = tools }), JsonSerializer.Serialize(skills.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Select(token => new { code = "unknown_competency", propertyName = token })));
    private static CompetencyBackfillPreview Preview() => new(BatchId, "Completed", 1, 1, 1, 0, 1, 1, 1, 0, 1, 0, 1, 0, 0, "safe-host", "safe-db", [], [new(Guid.NewGuid(), "AWS", "AWS", CompetencyCategory.Tool)]);
    private static CompetencyBackfillCommand Command(FakeExecution execution) => new(execution, new Confirmation(true), new Output());
    private sealed class FakeExecution : ICompetencyBackfillExecution
    {
        private int _preview; public int PreviewCalls { get; private set; } public int BackfillCalls { get; private set; } public (int, int) Counts { get; private set; } public Result<CompetencyBackfillPreview> Preview { get; set; } = Result<CompetencyBackfillPreview>.Success(Preview());
        public Task<Result<CompetencyBackfillPreview>> PreviewAsync(CompetencyBackfillOptions options, CancellationToken cancellationToken) { PreviewCalls++; return Task.FromResult(_preview++ == 0 ? Preview : Result<CompetencyBackfillPreview>.Success(Preview.Value with { ExpectedNewCompetencyCount = 0, ExpectedNewLinkCount = 0 })); }
        public Task<Result<CompetencyBackfillResult>> BackfillAsync(CompetencyBackfillOptions options, int expectedCompetencies, int expectedLinks, CancellationToken cancellationToken) { BackfillCalls++; Counts = (expectedCompetencies, expectedLinks); return Task.FromResult(Result<CompetencyBackfillResult>.Success(new(BatchId, 1, 1, 1, 1, 0, 1, 0, 0, 0, "Completed", DateTime.UtcNow, DateTime.UtcNow))); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class Confirmation(bool accepted) : ICompetencyBackfillConfirmation { public Task<bool> ConfirmAsync(string requiredText, string? competencies, string? links, CancellationToken cancellationToken) => Task.FromResult(accepted && competencies == "1" && links == "1"); }
    private sealed class Output : IImportOutput { public void WriteLine(string message) { } public void WriteError(string message) { } }
}
