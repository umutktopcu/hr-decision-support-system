using HrDecisionSupport.Application.CandidateEvaluations;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace HrDecisionSupport.Tests;

public class CandidateEvaluationCaseServiceTests
{
    private static readonly DateTimeOffset FixedUtc =
        new(2026, 7, 20, 11, 15, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_EmptyDatabase_ReturnsSuccessfulEmptyListWithoutTracking()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).ListAsync();
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Empty(context.ChangeTracker.Entries<CandidateEvaluationCase>());
    }

    [Fact]
    public async Task List_ProjectsSafeReferencesAndOrdersActiveThenReceivedDateThenId()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        var activeOlder = TestDatabase.CandidateEvaluationCase(
            graph.Candidate, graph.Requisition, CandidateEvaluationStatus.InReview,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        var activeNewer = TestDatabase.CandidateEvaluationCase(
            graph.Candidate2, graph.Requisition, CandidateEvaluationStatus.New,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var completedNewest = TestDatabase.CandidateEvaluationCase(
            graph.Candidate3, graph.Requisition, CandidateEvaluationStatus.Approved,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Person2, graph.Person3,
            graph.Candidate, graph.Candidate2, graph.Candidate3,
            activeOlder, activeNewer, completedNewest);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).ListAsync();

        Assert.Equal(
            [activeNewer.Id, activeOlder.Id, completedNewest.Id],
            result.Value.Select(item => item.Id));
        var item = result.Value[0];
        Assert.Equal(graph.Candidate2.CandidateCode, item.CandidateCode);
        Assert.Equal(graph.Person2.AnonymousCode, item.AnonymousCode);
        Assert.Equal(graph.Person2.FirstName, item.CandidateFirstName);
        Assert.Equal(graph.Requisition.RequisitionCode, item.RequisitionCode);
        Assert.Equal(graph.Department.Name, item.DepartmentName);
        Assert.Equal(graph.Position.Name, item.PositionName);
        Assert.Empty(context.ChangeTracker.Entries<CandidateEvaluationCase>());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Filter_EmptyId_ReturnsValidation(bool byCandidate)
    {
        await using var context = TestDatabase.CreateContext();
        Result result = byCandidate
            ? await Service(context).ListByCandidateAsync(Guid.Empty)
            : await Service(context).ListByRequisitionAsync(Guid.Empty);
        AssertError(
            result,
            byCandidate ? "candidate_id_required" : "job_requisition_id_required",
            ErrorType.Validation);
    }

    [Theory]
    [InlineData(true, "candidate_not_found")]
    [InlineData(false, "job_requisition_not_found")]
    public async Task Filter_MissingOwner_ReturnsNotFound(bool byCandidate, string code)
    {
        await using var context = TestDatabase.CreateContext();
        Result result = byCandidate
            ? await Service(context).ListByCandidateAsync(Guid.NewGuid())
            : await Service(context).ListByRequisitionAsync(Guid.NewGuid());
        AssertError(result, code, ErrorType.NotFound);
    }

    [Fact]
    public async Task ListByRequisition_ExistingWithoutCases_ReturnsEmptyList()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        context.AddRange(graph.Department, graph.Position, graph.Requisition);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).ListByRequisitionAsync(graph.Requisition.Id);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Empty(context.ChangeTracker.Entries<CandidateEvaluationCase>());
    }

    [Fact]
    public async Task ListByCandidate_ExistingWithoutCases_ReturnsEmptyList()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        context.AddRange(graph.Person, graph.Candidate);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).ListByCandidateAsync(graph.Candidate.Id);
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Empty(context.ChangeTracker.Entries<CandidateEvaluationCase>());
    }

    [Fact]
    public async Task FilterLists_ReturnOnlyMatchingCasesWithDeterministicProjection()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        var secondRequisition = TestDatabase.JobRequisition(
            graph.Department, graph.Position, status: JobRequisitionStatus.Open);
        var expected = TestDatabase.CandidateEvaluationCase(graph.Candidate, graph.Requisition);
        var otherCandidate = TestDatabase.CandidateEvaluationCase(graph.Candidate2, graph.Requisition);
        var otherRequisition = TestDatabase.CandidateEvaluationCase(graph.Candidate, secondRequisition);
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition, secondRequisition,
            graph.Person, graph.Person2, graph.Candidate, graph.Candidate2,
            expected, otherCandidate, otherRequisition);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var byRequisition = await Service(context).ListByRequisitionAsync(graph.Requisition.Id);
        var byCandidate = await Service(context).ListByCandidateAsync(graph.Candidate.Id);

        Assert.Equal(
            new[] { expected.Id, otherCandidate.Id }.OrderBy(id => id),
            byRequisition.Value.Select(item => item.Id).OrderBy(id => id));
        Assert.Equal(
            new[] { expected.Id, otherRequisition.Id }.OrderBy(id => id),
            byCandidate.Value.Select(item => item.Id).OrderBy(id => id));
        Assert.All(byRequisition.Value, item => Assert.Equal(graph.Requisition.Id, item.JobRequisitionId));
        Assert.All(byCandidate.Value, item => Assert.Equal(graph.Candidate.Id, item.CandidateId));
        Assert.Empty(context.ChangeTracker.Entries<CandidateEvaluationCase>());
    }

    [Fact]
    public async Task ListByRequisition_ReturnsActualDeterministicOrderWithoutResorting()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        var activeOlder = TestDatabase.CandidateEvaluationCase(
            graph.Candidate, graph.Requisition, CandidateEvaluationStatus.InReview,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        var activeNewer = TestDatabase.CandidateEvaluationCase(
            graph.Candidate2, graph.Requisition, CandidateEvaluationStatus.New,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var completedNewest = TestDatabase.CandidateEvaluationCase(
            graph.Candidate3, graph.Requisition, CandidateEvaluationStatus.Approved,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Person2, graph.Person3,
            graph.Candidate, graph.Candidate2, graph.Candidate3,
            activeOlder, activeNewer, completedNewest);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).ListByRequisitionAsync(graph.Requisition.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [activeNewer.Id, activeOlder.Id, completedNewest.Id],
            result.Value.Select(item => item.Id));
    }

    [Fact]
    public async Task ListByCandidate_ReturnsActualDeterministicOrderWithoutResorting()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        var secondRequisition = TestDatabase.JobRequisition(
            graph.Department, graph.Position, status: JobRequisitionStatus.Open);
        var thirdRequisition = TestDatabase.JobRequisition(
            graph.Department, graph.Position, status: JobRequisitionStatus.Open);
        var activeOlder = TestDatabase.CandidateEvaluationCase(
            graph.Candidate, graph.Requisition, CandidateEvaluationStatus.Interview,
            new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc));
        var activeNewer = TestDatabase.CandidateEvaluationCase(
            graph.Candidate, secondRequisition, CandidateEvaluationStatus.InReview,
            new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var completedNewest = TestDatabase.CandidateEvaluationCase(
            graph.Candidate, thirdRequisition, CandidateEvaluationStatus.Rejected,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc));
        context.AddRange(
            graph.Department, graph.Position,
            graph.Requisition, secondRequisition, thirdRequisition,
            graph.Person, graph.Candidate,
            activeOlder, activeNewer, completedNewest);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).ListByCandidateAsync(graph.Candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [activeNewer.Id, activeOlder.Id, completedNewest.Id],
            result.Value.Select(item => item.Id));
    }

    [Fact]
    public async Task List_SameStatusAndReceivedDate_UsesIdAscendingTieBreaker()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        var received = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var smallerId = TestDatabase.CandidateEvaluationCase(
            graph.Candidate, graph.Requisition, CandidateEvaluationStatus.InReview, received);
        var largerId = TestDatabase.CandidateEvaluationCase(
            graph.Candidate2, graph.Requisition, CandidateEvaluationStatus.InReview, received);
        smallerId.Id = Guid.Parse("00000000-0000-0000-0000-000000000001");
        largerId.Id = Guid.Parse("00000000-0000-0000-0000-000000000002");
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Person2, graph.Candidate, graph.Candidate2,
            largerId, smallerId);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).ListAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [smallerId.Id, largerId.Id],
            result.Value.Select(item => item.Id));
    }

    [Fact]
    public async Task List_CancelledToken_IsPropagatedToEfQuery()
    {
        await using var context = TestDatabase.CreateContext();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => Service(context).ListAsync(cancellation.Token));
    }

    [Fact]
    public async Task Get_MissingCase_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(
            await Service(context).GetByIdAsync(Guid.NewGuid()),
            "candidate_evaluation_case_not_found",
            ErrorType.NotFound);
    }

    [Fact]
    public async Task Get_ExistingCase_ReturnsCompleteSafeDetailProjectionWithoutTracking()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        var evaluation = TestDatabase.CandidateEvaluationCase(
            graph.Candidate, graph.Requisition, CandidateEvaluationStatus.Interview,
            new DateTime(2026, 6, 5, 9, 0, 0, DateTimeKind.Utc));
        evaluation.ExternalReference = "ATS-42";
        evaluation.Notes = "Long detail";
        evaluation.UpdatedAtUtc = FixedUtc.UtcDateTime;
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Candidate, evaluation);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).GetByIdAsync(evaluation.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(graph.Candidate.CandidateCode, result.Value.CandidateCode);
        Assert.Equal(graph.Person.AnonymousCode, result.Value.AnonymousCode);
        Assert.Equal(graph.Requisition.Title, result.Value.RequisitionTitle);
        Assert.Equal(graph.Department.Id, result.Value.DepartmentId);
        Assert.Equal(graph.Position.Id, result.Value.PositionId);
        Assert.Equal(CandidateEvaluationStatus.Interview, result.Value.Status);
        Assert.Equal("ATS-42", result.Value.ExternalReference);
        Assert.Equal("Long detail", result.Value.Notes);
        Assert.Equal(FixedUtc.UtcDateTime, result.Value.UpdatedAtUtc);
        Assert.Empty(context.ChangeTracker.Entries<CandidateEvaluationCase>());
    }

    [Fact]
    public async Task Get_NullableStrings_ProjectsNullValuesSuccessfully()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        var evaluation = TestDatabase.CandidateEvaluationCase(graph.Candidate, graph.Requisition);
        evaluation.ExternalReference = null;
        evaluation.Notes = null;
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Candidate, evaluation);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).GetByIdAsync(evaluation.Id);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.ExternalReference);
        Assert.Null(result.Value.Notes);
        Assert.Empty(context.ChangeTracker.Entries<CandidateEvaluationCase>());
    }

    [Theory]
    [InlineData(JobRequisitionStatus.Open)]
    [InlineData(JobRequisitionStatus.OnHold)]
    public async Task Create_AvailableRequisition_CreatesNewCaseTrimsAndUsesTimeProvider(
        JobRequisitionStatus status)
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph(status);
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Candidate);
        await context.SaveChangesAsync();

        var received = new DateTime(2026, 7, 1, 8, 0, 0, DateTimeKind.Utc);
        var result = await Service(context).CreateAsync(
            new(graph.Candidate.Id, graph.Requisition.Id, "  REF-1  ", received, "  Notes  "));

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal(CandidateEvaluationStatus.New, result.Value.Status);
        Assert.Equal("REF-1", result.Value.ExternalReference);
        Assert.Equal("Notes", result.Value.Notes);
        Assert.Equal(received, result.Value.ReceivedAtUtc);
        Assert.Equal(FixedUtc.UtcDateTime, result.Value.CreatedAtUtc);
        Assert.Null(result.Value.UpdatedAtUtc);
        Assert.Single(context.CandidateEvaluationCases);
        Assert.Single(context.Candidates);
        Assert.Empty(context.Employees);
    }

    [Fact]
    public async Task Create_MissingCandidate_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph(JobRequisitionStatus.Open);
        context.AddRange(graph.Department, graph.Position, graph.Requisition);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).CreateAsync(ValidCreate(Guid.NewGuid(), graph.Requisition.Id)),
            "candidate_not_found",
            ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_CandidateWhosePersonIsMissing_ReturnsSafeNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph(JobRequisitionStatus.Open);
        context.AddRange(graph.Department, graph.Position, graph.Requisition, graph.Candidate);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).CreateAsync(ValidCreate(graph.Candidate.Id, graph.Requisition.Id)),
            "candidate_not_found",
            ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_MissingRequisition_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        context.AddRange(graph.Person, graph.Candidate);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).CreateAsync(ValidCreate(graph.Candidate.Id, Guid.NewGuid())),
            "job_requisition_not_found",
            ErrorType.NotFound);
    }

    [Theory]
    [InlineData(JobRequisitionStatus.Draft)]
    [InlineData(JobRequisitionStatus.Closed)]
    [InlineData(JobRequisitionStatus.Cancelled)]
    public async Task Create_UnavailableRequisitionStatus_ReturnsConflict(
        JobRequisitionStatus status)
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph(status);
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Candidate);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).CreateAsync(ValidCreate(graph.Candidate.Id, graph.Requisition.Id)),
            "candidate_evaluation_case_requisition_not_available",
            ErrorType.Conflict);
    }

    [Fact]
    public async Task Create_DuplicateCandidateAndRequisition_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph(JobRequisitionStatus.Open);
        var existing = TestDatabase.CandidateEvaluationCase(graph.Candidate, graph.Requisition);
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Candidate, existing);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).CreateAsync(ValidCreate(graph.Candidate.Id, graph.Requisition.Id)),
            "candidate_evaluation_case_conflict",
            ErrorType.Conflict);
    }

    [Fact]
    public async Task Create_SameRequisitionWithDifferentCandidateAndSameCandidateWithDifferentRequisition_AreAllowed()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph(JobRequisitionStatus.Open);
        var secondRequisition = TestDatabase.JobRequisition(
            graph.Department, graph.Position, status: JobRequisitionStatus.Open);
        var existing = TestDatabase.CandidateEvaluationCase(graph.Candidate, graph.Requisition);
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition, secondRequisition,
            graph.Person, graph.Person2, graph.Candidate, graph.Candidate2, existing);
        await context.SaveChangesAsync();

        Assert.True((await Service(context).CreateAsync(
            ValidCreate(graph.Candidate2.Id, graph.Requisition.Id))).IsSuccess);
        Assert.True((await Service(context).CreateAsync(
            ValidCreate(graph.Candidate.Id, secondRequisition.Id))).IsSuccess);
    }

    [Fact]
    public async Task Create_InvalidRequest_ReturnsAllValidationErrorsAndCreatesNothing()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).CreateAsync(
            new(Guid.Empty, Guid.Empty, " ", default, " "));
        Assert.False(result.IsSuccess);
        Assert.Equal(5, result.Errors.Count);
        Assert.All(result.Errors, error => Assert.Equal(ErrorType.Validation, error.Type));
        Assert.Empty(context.CandidateEvaluationCases);
    }

    [Fact]
    public async Task Update_ValidMutableFields_PreservesIdentityForeignKeysAndStatus()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph(JobRequisitionStatus.Cancelled);
        var evaluation = TestDatabase.CandidateEvaluationCase(
            graph.Candidate, graph.Requisition, CandidateEvaluationStatus.InReview);
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Candidate, evaluation);
        await context.SaveChangesAsync();
        var originalCandidateId = evaluation.CandidateId;
        var originalRequisitionId = evaluation.JobRequisitionId;

        var received = new DateTime(2026, 7, 2, 8, 0, 0, DateTimeKind.Utc);
        var result = await Service(context).UpdateAsync(
            evaluation.Id, new("  UPDATED  ", received, "  Updated notes  "));

        Assert.True(result.IsSuccess);
        Assert.Equal(originalCandidateId, result.Value.CandidateId);
        Assert.Equal(originalRequisitionId, result.Value.JobRequisitionId);
        Assert.Equal(CandidateEvaluationStatus.InReview, result.Value.Status);
        Assert.Equal("UPDATED", result.Value.ExternalReference);
        Assert.Equal("Updated notes", result.Value.Notes);
        Assert.Equal(FixedUtc.UtcDateTime, result.Value.UpdatedAtUtc);
    }

    [Fact]
    public async Task Update_MissingCase_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(
            await Service(context).UpdateAsync(Guid.NewGuid(), ValidUpdate()),
            "candidate_evaluation_case_not_found",
            ErrorType.NotFound);
    }

    [Fact]
    public async Task Update_InvalidRequest_LeavesEntityUnchanged()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        var evaluation = TestDatabase.CandidateEvaluationCase(graph.Candidate, graph.Requisition);
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Candidate, evaluation);
        await context.SaveChangesAsync();
        var originalReference = evaluation.ExternalReference;
        var originalReceived = evaluation.ReceivedAtUtc;

        var result = await Service(context).UpdateAsync(
            evaluation.Id, new(" ", default, " "));

        Assert.False(result.IsSuccess);
        Assert.Equal(originalReference, evaluation.ExternalReference);
        Assert.Equal(originalReceived, evaluation.ReceivedAtUtc);
        Assert.Null(evaluation.UpdatedAtUtc);
    }

    [Theory]
    [InlineData(CandidateEvaluationStatus.Approved)]
    [InlineData(CandidateEvaluationStatus.Rejected)]
    [InlineData(CandidateEvaluationStatus.Withdrawn)]
    [InlineData(CandidateEvaluationStatus.Closed)]
    public async Task Update_TerminalCase_ReturnsLockedConflict(CandidateEvaluationStatus status)
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        var evaluation = TestDatabase.CandidateEvaluationCase(graph.Candidate, graph.Requisition, status);
        evaluation.CreatedAtUtc = new DateTime(2026, 5, 1, 8, 0, 0, DateTimeKind.Utc);
        evaluation.UpdatedAtUtc = new DateTime(2026, 5, 2, 8, 0, 0, DateTimeKind.Utc);
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Candidate, evaluation);
        await context.SaveChangesAsync();
        var original = Snapshot(evaluation);

        AssertError(
            await Service(context).UpdateAsync(
                evaluation.Id,
                new(
                    "CHANGED",
                    new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc),
                    "Changed notes")),
            "candidate_evaluation_case_locked",
            ErrorType.Conflict);
        AssertUnchanged(evaluation, original);
    }

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public async Task ChangeStatus_ValidTransition_ChangesOnlyStatusAndAuditTimestamp(
        CandidateEvaluationStatus current,
        CandidateEvaluationStatus next)
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph(JobRequisitionStatus.Closed);
        var evaluation = TestDatabase.CandidateEvaluationCase(graph.Candidate, graph.Requisition, current);
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Candidate, evaluation);
        await context.SaveChangesAsync();
        var original = Snapshot(evaluation);

        var result = await Service(context).ChangeStatusAsync(evaluation.Id, new(next));

        Assert.True(result.IsSuccess);
        Assert.Equal(next, result.Value.Status);
        AssertStructuralFieldsUnchanged(evaluation, original);
        Assert.Equal(next, evaluation.Status);
        Assert.NotEqual(original.UpdatedAtUtc, evaluation.UpdatedAtUtc);
        Assert.Equal(FixedUtc.UtcDateTime, result.Value.UpdatedAtUtc);
    }

    [Fact]
    public async Task ChangeStatus_AllInvalidDifferentTransitions_ReturnConflictAndLeaveStatusUnchanged()
    {
        foreach (var current in Enum.GetValues<CandidateEvaluationStatus>())
        foreach (var next in Enum.GetValues<CandidateEvaluationStatus>())
        {
            if (current == next || ValidTransitionSet.Contains((current, next)))
                continue;

            await using var context = TestDatabase.CreateContext();
            var graph = Graph();
            var evaluation = TestDatabase.CandidateEvaluationCase(graph.Candidate, graph.Requisition, current);
            context.AddRange(
                graph.Department, graph.Position, graph.Requisition,
                graph.Person, graph.Candidate, evaluation);
            await context.SaveChangesAsync();

            AssertError(
                await Service(context).ChangeStatusAsync(evaluation.Id, new(next)),
                "candidate_evaluation_case_status_transition_invalid",
                ErrorType.Conflict);
            Assert.Equal(current, evaluation.Status);
            Assert.Null(evaluation.UpdatedAtUtc);
        }
    }

    [Theory]
    [InlineData(CandidateEvaluationStatus.New)]
    [InlineData(CandidateEvaluationStatus.InReview)]
    [InlineData(CandidateEvaluationStatus.Interview)]
    [InlineData(CandidateEvaluationStatus.Approved)]
    [InlineData(CandidateEvaluationStatus.Rejected)]
    [InlineData(CandidateEvaluationStatus.Withdrawn)]
    [InlineData(CandidateEvaluationStatus.Closed)]
    public async Task ChangeStatus_SameStatus_ReturnsNoChangeConflictAndLeavesEntityUnchanged(
        CandidateEvaluationStatus status)
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        var evaluation = TestDatabase.CandidateEvaluationCase(graph.Candidate, graph.Requisition, status);
        evaluation.UpdatedAtUtc = new DateTime(2026, 5, 2, 8, 0, 0, DateTimeKind.Utc);
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Candidate, evaluation);
        await context.SaveChangesAsync();
        var original = Snapshot(evaluation);

        AssertError(
            await Service(context).ChangeStatusAsync(evaluation.Id, new(evaluation.Status)),
            "candidate_evaluation_case_status_no_change",
            ErrorType.Conflict);
        AssertUnchanged(evaluation, original);
    }

    [Fact]
    public async Task ChangeStatus_InvalidEnum_ReturnsValidationAndLeavesEntityUnchanged()
    {
        await using var context = TestDatabase.CreateContext();
        var graph = Graph();
        var evaluation = TestDatabase.CandidateEvaluationCase(graph.Candidate, graph.Requisition);
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Candidate, evaluation);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).ChangeStatusAsync(evaluation.Id, new((CandidateEvaluationStatus)999)),
            "candidate_evaluation_case_status_invalid",
            ErrorType.Validation);
        Assert.Equal(CandidateEvaluationStatus.New, evaluation.Status);
    }

    [Fact]
    public async Task ChangeStatus_MissingCase_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(
            await Service(context).ChangeStatusAsync(
                Guid.NewGuid(), new(CandidateEvaluationStatus.InReview)),
            "candidate_evaluation_case_not_found",
            ErrorType.NotFound);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task SuccessfulMutation_CallsSaveChangesExactlyOnce(int operation)
    {
        var interceptor = new SaveCounterInterceptor();
        var options = new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseInMemoryDatabase($"evaluation-save-count-{Guid.NewGuid():N}")
            .AddInterceptors(interceptor)
            .Options;
        await using var context = new HrDecisionSupportDbContext(options);
        var graph = Graph(JobRequisitionStatus.Open);
        var evaluation = TestDatabase.CandidateEvaluationCase(graph.Candidate, graph.Requisition);
        context.AddRange(
            graph.Department, graph.Position, graph.Requisition,
            graph.Person, graph.Candidate);
        if (operation != 0)
            context.Add(evaluation);
        await context.SaveChangesAsync();
        interceptor.Count = 0;

        Result result = operation switch
        {
            0 => await Service(context).CreateAsync(
                ValidCreate(graph.Candidate.Id, graph.Requisition.Id)),
            1 => await Service(context).UpdateAsync(evaluation.Id, ValidUpdate()),
            _ => await Service(context).ChangeStatusAsync(
                evaluation.Id, new(CandidateEvaluationStatus.InReview))
        };

        Assert.True(result.IsSuccess);
        Assert.Equal(1, interceptor.Count);
    }

    public static TheoryData<CandidateEvaluationStatus, CandidateEvaluationStatus> ValidTransitions =>
        new()
        {
            { CandidateEvaluationStatus.New, CandidateEvaluationStatus.InReview },
            { CandidateEvaluationStatus.New, CandidateEvaluationStatus.Rejected },
            { CandidateEvaluationStatus.New, CandidateEvaluationStatus.Withdrawn },
            { CandidateEvaluationStatus.New, CandidateEvaluationStatus.Closed },
            { CandidateEvaluationStatus.InReview, CandidateEvaluationStatus.Interview },
            { CandidateEvaluationStatus.InReview, CandidateEvaluationStatus.Approved },
            { CandidateEvaluationStatus.InReview, CandidateEvaluationStatus.Rejected },
            { CandidateEvaluationStatus.InReview, CandidateEvaluationStatus.Withdrawn },
            { CandidateEvaluationStatus.InReview, CandidateEvaluationStatus.Closed },
            { CandidateEvaluationStatus.Interview, CandidateEvaluationStatus.Approved },
            { CandidateEvaluationStatus.Interview, CandidateEvaluationStatus.Rejected },
            { CandidateEvaluationStatus.Interview, CandidateEvaluationStatus.Withdrawn },
            { CandidateEvaluationStatus.Interview, CandidateEvaluationStatus.Closed }
        };

    private static readonly HashSet<(CandidateEvaluationStatus, CandidateEvaluationStatus)>
        ValidTransitionSet = ValidTransitions
            .Select(row => ((CandidateEvaluationStatus)row[0], (CandidateEvaluationStatus)row[1]))
            .ToHashSet();

    private static CandidateEvaluationCaseService Service(HrDecisionSupportDbContext context) =>
        new(
            context,
            new CreateCandidateEvaluationCaseRequestValidator(),
            new UpdateCandidateEvaluationCaseRequestValidator(),
            new ChangeCandidateEvaluationCaseStatusRequestValidator(),
            new FixedTimeProvider(FixedUtc));

    private static CreateCandidateEvaluationCaseRequest ValidCreate(
        Guid candidateId,
        Guid requisitionId) =>
        new(candidateId, requisitionId, null,
            new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc), null);

    private static UpdateCandidateEvaluationCaseRequest ValidUpdate() =>
        new(null, new DateTime(2026, 7, 2, 0, 0, 0, DateTimeKind.Utc), null);

    private static TestGraph Graph(JobRequisitionStatus status = JobRequisitionStatus.Open)
    {
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(department, position, status: status);
        var person = TestDatabase.Person("ANON-1");
        var person2 = TestDatabase.Person("ANON-2", "Grace", "Hopper");
        var person3 = TestDatabase.Person("ANON-3", "Katherine", "Johnson");
        return new(
            department,
            position,
            requisition,
            person,
            TestDatabase.Candidate(person, "CAN-1"),
            person2,
            TestDatabase.Candidate(person2, "CAN-2"),
            person3,
            TestDatabase.Candidate(person3, "CAN-3"));
    }

    private static void AssertError(Result result, string code, ErrorType type)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(code, Assert.Single(result.Errors).Code);
        Assert.Equal(type, result.Error!.Type);
    }

    private static EvaluationSnapshot Snapshot(CandidateEvaluationCase evaluation) =>
        new(
            evaluation.Id,
            evaluation.CandidateId,
            evaluation.JobRequisitionId,
            evaluation.ExternalReference,
            evaluation.ReceivedAtUtc,
            evaluation.Notes,
            evaluation.Status,
            evaluation.CreatedAtUtc,
            evaluation.UpdatedAtUtc);

    private static void AssertStructuralFieldsUnchanged(
        CandidateEvaluationCase evaluation,
        EvaluationSnapshot original)
    {
        Assert.Equal(original.Id, evaluation.Id);
        Assert.Equal(original.CandidateId, evaluation.CandidateId);
        Assert.Equal(original.JobRequisitionId, evaluation.JobRequisitionId);
        Assert.Equal(original.ExternalReference, evaluation.ExternalReference);
        Assert.Equal(original.ReceivedAtUtc, evaluation.ReceivedAtUtc);
        Assert.Equal(original.Notes, evaluation.Notes);
        Assert.Equal(original.CreatedAtUtc, evaluation.CreatedAtUtc);
    }

    private static void AssertUnchanged(
        CandidateEvaluationCase evaluation,
        EvaluationSnapshot original)
    {
        AssertStructuralFieldsUnchanged(evaluation, original);
        Assert.Equal(original.Status, evaluation.Status);
        Assert.Equal(original.UpdatedAtUtc, evaluation.UpdatedAtUtc);
    }

    private sealed record TestGraph(
        Department Department,
        Position Position,
        JobRequisition Requisition,
        Person Person,
        Candidate Candidate,
        Person Person2,
        Candidate Candidate2,
        Person Person3,
        Candidate Candidate3);

    private sealed record EvaluationSnapshot(
        Guid Id,
        Guid CandidateId,
        Guid JobRequisitionId,
        string? ExternalReference,
        DateTime ReceivedAtUtc,
        string? Notes,
        CandidateEvaluationStatus Status,
        DateTime CreatedAtUtc,
        DateTime? UpdatedAtUtc);

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class SaveCounterInterceptor : SaveChangesInterceptor
    {
        internal int Count { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Count++;
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
