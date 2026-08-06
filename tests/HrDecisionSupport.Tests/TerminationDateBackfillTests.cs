using HrDecisionSupport.Application.Common;
using HrDecisionSupport.EmployeeImportDryRun;

namespace HrDecisionSupport.Tests;

public sealed class TerminationDateBackfillTests
{
    private static readonly Guid BatchId = Guid.Parse("313236a9-2016-4dd2-992a-078d71cccfdd");
    [Fact]
    public void Planner_ClassifiesAllSafetyCasesAndOnlyReturnsSafeCandidate()
    {
        var employee = Guid.NewGuid(); var date = new DateOnly(2024, 6, 30);
        var plan = new TerminationDateBackfillPlanner().Plan(BatchId, "Completed", [Row(employee, "", null), Row(Guid.NewGuid(), "2024-06-30", date), Row(Guid.NewGuid(), "2024-06-30", null), Row(Guid.NewGuid(), "2024-06-30", new DateOnly(2024, 1, 1)), Row(Guid.NewGuid(), "bad-date", null), new(Guid.NewGuid(), 7, null, null, null, "2024-06-30"), Row(Guid.NewGuid(), "2019-01-01", null)], "safe-host", "safe-db");
        Assert.Equal(7, plan.TotalImportRows); Assert.Equal(1, plan.Count(TerminationDateBackfillCategory.SourceNullDbNull)); Assert.Equal(1, plan.Count(TerminationDateBackfillCategory.SourceMatchesDb)); Assert.Equal(2, plan.Count(TerminationDateBackfillCategory.SourceFilledDbNull)); Assert.Equal(1, plan.Count(TerminationDateBackfillCategory.SourceDiffersDb)); Assert.Equal(1, plan.Count(TerminationDateBackfillCategory.SourceUnparseable)); Assert.Equal(1, plan.Count(TerminationDateBackfillCategory.EmployeeUnmatched)); Assert.Single(plan.Candidates); Assert.Equal(date, plan.Candidates.Single().SourceTerminationDate);
    }
    [Fact]
    public void Planner_ConflictingSourceDatesAreNotCandidates()
    {
        var employee = Guid.NewGuid(); var plan = new TerminationDateBackfillPlanner().Plan(BatchId, "Completed", [Row(employee, "2024-01-01", null, 2), Row(employee, "2024-02-01", null, 3)], "h", "d");
        Assert.Equal(0, plan.CandidateCount); Assert.Equal(2, plan.ManualReviewCount);
    }
    [Theory]
    [InlineData("2024-06-30", true)]
    [InlineData("30.06.2024", true)]
    [InlineData("30.6.2024", true)]
    [InlineData("not-a-date", false)]
    public void Planner_UsesSpreadsheetCompatibleDateParsing(string value, bool valid) => Assert.Equal(valid, TerminationDateBackfillPlanner.Parse(value).HasValue);
    [Fact]
    public async Task Command_MissingBatchRejectsBeforePreview()
    {
        var execution = new FakeExecution(); var exit = await Command(execution).ExecuteAsync(new(null, "1", "Development")); Assert.Equal(1, exit); Assert.Equal(0, execution.PreviewCalls);
    }
    [Fact]
    public async Task Command_UnknownBatchRejectsWithoutBackfill()
    {
        var execution = new FakeExecution { Preview = Result<TerminationDateBackfillPreview>.Failure("termination_date_backfill.batch_not_found", "missing") }; var exit = await Command(execution).ExecuteAsync(new(BatchId, "1", "Development")); Assert.Equal(1, exit); Assert.Equal(0, execution.BackfillCalls);
    }
    [Fact]
    public async Task Command_ProductionIsLockedBeforePreview()
    {
        var execution = new FakeExecution(); var exit = await Command(execution).ExecuteAsync(new(BatchId, "1", "Production")); Assert.Equal(1, exit); Assert.Equal(0, execution.PreviewCalls);
    }
    [Fact]
    public async Task Command_WrongConfirmationRejectsWithoutUpdate()
    {
        var execution = new FakeExecution(); var exit = await new TerminationDateBackfillCommand(execution, new Confirmation(false), new Output()).ExecuteAsync(new(BatchId, "1", "Development")); Assert.Equal(1, exit); Assert.Equal(0, execution.BackfillCalls);
    }
    [Fact]
    public async Task ConsoleConfirmation_RequiresExactBackfillCandidateCount()
    {
        var confirmation = new ConsoleImportConfirmation(); Assert.True(await confirmation.ConfirmAsync("BACKFILL 2327", "2327", CancellationToken.None)); Assert.False(await confirmation.ConfirmAsync("BACKFILL 2327", "2326", CancellationToken.None));
    }
    [Fact]
    public async Task Command_CorrectConfirmationRunsAndSecondPreviewIsZero()
    {
        var execution = new FakeExecution(); var exit = await Command(execution).ExecuteAsync(new(BatchId, "1", "Development")); Assert.Equal(0, exit); Assert.Equal(1, execution.BackfillCalls); Assert.Equal(2, execution.PreviewCalls); Assert.Equal(1, execution.ExpectedCandidates);
    }
    private static TerminationDateBackfillRow Row(Guid employee, string? source, DateOnly? persisted, int sourceRow = 2) => new(Guid.NewGuid(), sourceRow, employee, new DateOnly(2020, 1, 1), persisted, source);
    private static TerminationDateBackfillCommand Command(FakeExecution execution) => new(execution, new Confirmation(true), new Output());
    private sealed class FakeExecution : ITerminationDateBackfillExecution
    {
        private int _preview; public int PreviewCalls { get; private set; } public int BackfillCalls { get; private set; } public int ExpectedCandidates { get; private set; }
        public Result<TerminationDateBackfillPreview> Preview { get; set; } = Result<TerminationDateBackfillPreview>.Success(new(BatchId, "Completed", 1, new Dictionary<TerminationDateBackfillCategory, int> { [TerminationDateBackfillCategory.SourceFilledDbNull] = 1 }, 1, 1, 0, "safe-host", "safe-db", [new(Guid.NewGuid(), Guid.NewGuid(), new DateOnly(2024, 6, 30))]));
        public Task<Result<TerminationDateBackfillPreview>> PreviewAsync(TerminationDateBackfillOptions options, CancellationToken cancellationToken) { PreviewCalls++; if (_preview++ > 0) return Task.FromResult(Result<TerminationDateBackfillPreview>.Success(Preview.Value with { CandidateCount = 0, CandidateDistinctEmployees = 0, Candidates = [] })); return Task.FromResult(Preview); }
        public Task<Result<TerminationDateBackfillResult>> BackfillAsync(TerminationDateBackfillOptions options, int expectedCandidateCount, CancellationToken cancellationToken) { BackfillCalls++; ExpectedCandidates = expectedCandidateCount; return Task.FromResult(Result<TerminationDateBackfillResult>.Success(new(1, 0, 0, DateTime.UtcNow, DateTime.UtcNow))); }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class Confirmation(bool approved) : IImportConfirmation { public Task<bool> ConfirmAsync(string requiredText, string? suppliedRowCount, CancellationToken cancellationToken) => Task.FromResult(approved && suppliedRowCount == requiredText["BACKFILL ".Length..]); }
    private sealed class Output : IImportOutput { public void WriteLine(string message) { } public void WriteError(string message) { } }
}
