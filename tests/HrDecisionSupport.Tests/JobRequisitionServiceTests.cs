using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Requisitions;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using HrDecisionSupport.Infrastructure.Persistence;

namespace HrDecisionSupport.Tests;

public class JobRequisitionServiceTests
{
    private static readonly DateTimeOffset FixedUtc =
        new(2026, 6, 15, 10, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task List_EmptyDatabase_ReturnsSuccessfulEmptyListWithoutTracking()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).ListAsync();
        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task List_ProjectsReferencesAndRequirementCountAndOrdersDeterministically()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var competency = TestDatabase.Competency();
        var olderOpen = TestDatabase.JobRequisition(
            department, position, "REQ-OPEN-OLD", JobRequisitionStatus.Open, new(2026, 1, 1));
        var newerOpen = TestDatabase.JobRequisition(
            department, position, "REQ-OPEN-NEW", JobRequisitionStatus.Open, new(2026, 2, 1));
        var newestDraft = TestDatabase.JobRequisition(
            department, position, "REQ-DRAFT", JobRequisitionStatus.Draft, new(2026, 3, 1));
        context.AddRange(
            department, position, competency, olderOpen, newerOpen, newestDraft,
            TestDatabase.JobRequisitionRequirement(newerOpen, competency));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).ListAsync();

        Assert.Equal(
            ["REQ-OPEN-NEW", "REQ-OPEN-OLD", "REQ-DRAFT"],
            result.Value.Select(item => item.RequisitionCode));
        var projected = result.Value[0];
        Assert.Equal(department.Code, projected.DepartmentCode);
        Assert.Equal(department.Name, projected.DepartmentName);
        Assert.Equal(position.Code, projected.PositionCode);
        Assert.Equal(position.Name, projected.PositionName);
        Assert.Equal(1, projected.RequirementCount);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Get_ExistingRequisition_ReturnsOrderedRequirementsWithoutTracking()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(department, position);
        var alpha = TestDatabase.Competency("Alpha");
        var zulu = TestDatabase.Competency("Zulu");
        context.AddRange(
            department, position, requisition, alpha, zulu,
            TestDatabase.JobRequisitionRequirement(requisition, zulu, false),
            TestDatabase.JobRequisitionRequirement(requisition, alpha));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var result = await Service(context).GetByIdAsync(requisition.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(requisition.Id, result.Value.Id);
        Assert.Equal(["Alpha", "Zulu"], result.Value.Requirements.Select(item => item.CompetencyName));
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Get_MissingRequisition_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(
            await Service(context).GetByIdAsync(Guid.NewGuid()),
            "job_requisition_not_found",
            ErrorType.NotFound);
    }

    [Fact]
    public async Task Create_ValidRequest_TrimsStringsUsesDraftAndCreatesOnlyRequisition()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        context.AddRange(department, position);
        await context.SaveChangesAsync();

        var result = await Service(context).CreateAsync(
            new("  REQ-100  ", "  Engineer  ", department.Id, position.Id, "  Notes  ", 2, null, new(2026, 4, 1), null));

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("REQ-100", result.Value.RequisitionCode);
        Assert.Equal("Engineer", result.Value.Title);
        Assert.Equal("Notes", result.Value.Description);
        Assert.Equal(JobRequisitionStatus.Draft, result.Value.JobRequisitionStatus);
        Assert.Equal(FixedUtc.UtcDateTime, result.Value.CreatedAtUtc);
        Assert.Null(result.Value.UpdatedAtUtc);
        Assert.Single(context.JobRequisitions);
        Assert.Empty(context.Candidates);
        Assert.Empty(context.CandidateEvaluationCases);
    }

    [Theory]
    [InlineData(true, false, "department_not_found", ErrorType.NotFound)]
    [InlineData(false, true, "position_not_found", ErrorType.NotFound)]
    public async Task Create_MissingReference_ReturnsExpectedError(
        bool missingDepartment,
        bool missingPosition,
        string code,
        ErrorType type)
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        if (!missingDepartment) context.Departments.Add(department);
        if (!missingPosition) context.Positions.Add(position);
        await context.SaveChangesAsync();

        AssertError(await Service(context).CreateAsync(ValidCreate(department.Id, position.Id)), code, type);
    }

    [Theory]
    [InlineData(true, false, "department_inactive")]
    [InlineData(false, true, "position_inactive")]
    public async Task Create_InactiveReference_ReturnsFailure(
        bool inactiveDepartment,
        bool inactivePosition,
        string code)
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department(!inactiveDepartment);
        var position = TestDatabase.Position(!inactivePosition);
        context.AddRange(department, position);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).CreateAsync(ValidCreate(department.Id, position.Id)),
            code,
            ErrorType.Failure);
    }

    [Fact]
    public async Task Create_DuplicateCode_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        context.AddRange(department, position, TestDatabase.JobRequisition(department, position, "REQ-NEW"));
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).CreateAsync(ValidCreate(department.Id, position.Id)),
            "job_requisition_conflict",
            ErrorType.Conflict);
    }

    [Fact]
    public async Task Create_InvalidRequest_ReturnsAllValidationErrorsBeforeDatabaseQueries()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await Service(context).CreateAsync(
            new("   ", "", Guid.Empty, Guid.Empty, "   ", 0, null, default, null));
        Assert.False(result.IsSuccess);
        Assert.Equal(7, result.Errors.Count);
        Assert.All(result.Errors, error => Assert.Equal(ErrorType.Validation, error.Type));
    }

    [Fact]
    public async Task Update_ValidRequest_PreservesIdAndStatusAndReturnsCurrentProjection()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(
            department, position, status: JobRequisitionStatus.Open);
        context.AddRange(department, position, requisition);
        await context.SaveChangesAsync();

        var result = await Service(context).UpdateAsync(
            requisition.Id,
            new("  REQ-UPDATED  ", "  Updated  ", department.Id, position.Id, "  Detail  ", 4, null, new(2026, 2, 1)));

        Assert.True(result.IsSuccess);
        Assert.Equal(requisition.Id, result.Value.Id);
        Assert.Equal(JobRequisitionStatus.Open, result.Value.JobRequisitionStatus);
        Assert.Equal("REQ-UPDATED", result.Value.RequisitionCode);
        Assert.Equal("Updated", result.Value.Title);
        Assert.Equal("Detail", result.Value.Description);
        Assert.Equal(4, result.Value.OpeningsCount);
        Assert.Equal(FixedUtc.UtcDateTime, result.Value.UpdatedAtUtc);
    }

    [Fact]
    public async Task Update_MissingRequisition_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(
            await Service(context).UpdateAsync(Guid.NewGuid(), ValidUpdate(Guid.NewGuid(), Guid.NewGuid())),
            "job_requisition_not_found",
            ErrorType.NotFound);
    }

    [Theory]
    [InlineData(true, false, "department_not_found")]
    [InlineData(false, true, "position_not_found")]
    public async Task Update_MissingReference_ReturnsNotFoundWithoutMutation(
        bool missingDepartment,
        bool missingPosition,
        string expectedCode)
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(department, position);
        context.AddRange(department, position, requisition);
        await context.SaveChangesAsync();
        var originalCode = requisition.RequisitionCode;

        var request = ValidUpdate(
            missingDepartment ? Guid.NewGuid() : department.Id,
            missingPosition ? Guid.NewGuid() : position.Id);
        AssertError(
            await Service(context).UpdateAsync(requisition.Id, request),
            expectedCode,
            ErrorType.NotFound);
        Assert.Equal(originalCode, requisition.RequisitionCode);
        Assert.Equal(department.Id, requisition.DepartmentId);
        Assert.Equal(position.Id, requisition.PositionId);
    }

    [Fact]
    public async Task Update_DuplicateRequisitionCode_ReturnsConflictWithoutMutation()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(department, position, "REQ-ORIGINAL");
        var duplicate = TestDatabase.JobRequisition(department, position, "REQ-DUPLICATE");
        context.AddRange(department, position, requisition, duplicate);
        await context.SaveChangesAsync();

        AssertError(
            await Service(context).UpdateAsync(
                requisition.Id,
                new("REQ-DUPLICATE", "Updated", department.Id, position.Id, null, 1, null, new(2026, 1, 1))),
            "job_requisition_conflict",
            ErrorType.Conflict);
        Assert.Equal("REQ-ORIGINAL", requisition.RequisitionCode);
    }

    [Fact]
    public async Task Update_InvalidRequest_ReturnsValidationWithoutMutation()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(department, position, "REQ-UNCHANGED");
        context.AddRange(department, position, requisition);
        await context.SaveChangesAsync();
        var originalTitle = requisition.Title;
        var originalOpenings = requisition.OpeningsCount;

        var result = await Service(context).UpdateAsync(
            requisition.Id,
            new(" ", "", Guid.Empty, Guid.Empty, " ", 0, null, default));

        Assert.True(result.IsFailure);
        Assert.All(result.Errors, error => Assert.Equal(ErrorType.Validation, error.Type));
        Assert.Equal("REQ-UNCHANGED", requisition.RequisitionCode);
        Assert.Equal(originalTitle, requisition.Title);
        Assert.Equal(originalOpenings, requisition.OpeningsCount);
        Assert.Equal(department.Id, requisition.DepartmentId);
        Assert.Equal(position.Id, requisition.PositionId);
    }

    [Theory]
    [InlineData(JobRequisitionStatus.Closed)]
    [InlineData(JobRequisitionStatus.Cancelled)]
    public async Task Update_TerminalRequisition_ReturnsLocked(JobRequisitionStatus status)
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(
            department, position, status: status, closedAt: new(2026, 2, 1));
        context.AddRange(department, position, requisition);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).UpdateAsync(requisition.Id, ValidUpdate(department.Id, position.Id)),
            "job_requisition_locked",
            ErrorType.Conflict);
    }

    [Fact]
    public async Task Update_WithLinkedEvaluationCase_BlocksDepartmentOrPositionChange()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var newDepartment = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(department, position);
        var person = TestDatabase.Person("REQ-LINKED");
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(), PersonId = person.Id, CandidateCode = "CAN-LINKED",
            CandidateSource = CandidateSource.Other
        };
        var evaluation = new CandidateEvaluationCase
        {
            Id = Guid.NewGuid(), CandidateId = candidate.Id, JobRequisitionId = requisition.Id,
            ReceivedAtUtc = FixedUtc.UtcDateTime, Status = CandidateEvaluationStatus.New,
            CreatedAtUtc = FixedUtc.UtcDateTime
        };
        context.AddRange(department, newDepartment, position, requisition, person, candidate, evaluation);
        await context.SaveChangesAsync();

        AssertError(
            await Service(context).UpdateAsync(requisition.Id, ValidUpdate(newDepartment.Id, position.Id)),
            "job_requisition_locked",
            ErrorType.Conflict);
    }

    [Fact]
    public async Task Update_WithLinkedEvaluationCase_AllowsScalarChangesWhenReferencesStayTheSame()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(department, position, "REQ-SCALAR");
        context.AddRange(department, position, requisition);
        AddEvaluationCase(context, requisition, "SCALAR");
        await context.SaveChangesAsync();

        var result = await Service(context).UpdateAsync(
            requisition.Id,
            new("REQ-SCALAR-UPDATED", "Updated title", department.Id, position.Id,
                "Updated description", 5, null, new(2026, 2, 1)));

        Assert.True(result.IsSuccess);
        Assert.Equal("REQ-SCALAR-UPDATED", result.Value.RequisitionCode);
        Assert.Equal("Updated title", result.Value.Title);
        Assert.Equal("Updated description", result.Value.Description);
        Assert.Equal(5, result.Value.OpeningsCount);
        Assert.Equal(department.Id, result.Value.DepartmentId);
        Assert.Equal(position.Id, result.Value.PositionId);
        Assert.Equal(FixedUtc.UtcDateTime, result.Value.UpdatedAtUtc);
    }

    [Fact]
    public async Task Update_WithLinkedEvaluationCase_BlocksPositionChangeWithoutMutation()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var newPosition = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(department, position, "REQ-POSITION");
        context.AddRange(department, position, newPosition, requisition);
        AddEvaluationCase(context, requisition, "POSITION");
        await context.SaveChangesAsync();
        var originalTitle = requisition.Title;
        var originalOpenings = requisition.OpeningsCount;

        AssertError(
            await Service(context).UpdateAsync(
                requisition.Id, ValidUpdate(department.Id, newPosition.Id)),
            "job_requisition_locked",
            ErrorType.Conflict);
        Assert.Equal(position.Id, requisition.PositionId);
        Assert.Equal(department.Id, requisition.DepartmentId);
        Assert.Equal("REQ-POSITION", requisition.RequisitionCode);
        Assert.Equal(originalTitle, requisition.Title);
        Assert.Equal(originalOpenings, requisition.OpeningsCount);
    }

    [Theory]
    [MemberData(nameof(ValidTransitions))]
    public async Task ChangeStatus_AllValidTransitions_UpdateOnlyStatusMetadata(
        JobRequisitionStatus current,
        JobRequisitionStatus next,
        DateOnly? requestedClosedAt)
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(
            department, position, status: current, openedAt: new(2026, 1, 1));
        context.AddRange(department, position, requisition);
        await context.SaveChangesAsync();
        var originalCode = requisition.RequisitionCode;
        var originalTitle = requisition.Title;
        var originalDescription = requisition.Description;
        var originalOpenings = requisition.OpeningsCount;
        var originalOpenedAt = requisition.OpenedAt;
        var originalCreatedAtUtc = requisition.CreatedAtUtc;

        var result = await Service(context).ChangeStatusAsync(
            requisition.Id, new(next, requestedClosedAt));

        Assert.True(result.IsSuccess);
        Assert.Equal(next, result.Value.JobRequisitionStatus);
        Assert.Equal(requestedClosedAt, result.Value.ClosedAt);
        Assert.Equal(FixedUtc.UtcDateTime, result.Value.UpdatedAtUtc);
        Assert.Equal(originalCode, result.Value.RequisitionCode);
        Assert.Equal(originalTitle, result.Value.Title);
        Assert.Equal(originalDescription, result.Value.Description);
        Assert.Equal(originalOpenings, result.Value.OpeningsCount);
        Assert.Equal(originalOpenedAt, result.Value.OpenedAt);
        Assert.Equal(originalCreatedAtUtc, result.Value.CreatedAtUtc);
        Assert.Equal(department.Id, result.Value.DepartmentId);
        Assert.Equal(position.Id, result.Value.PositionId);
    }

    [Fact]
    public async Task ChangeStatus_SameStatus_ReturnsNoChangeConflict()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(department, position);
        context.AddRange(department, position, requisition);
        await context.SaveChangesAsync();
        var originalClosedAt = requisition.ClosedAt;
        AssertError(
            await Service(context).ChangeStatusAsync(requisition.Id, new(JobRequisitionStatus.Draft, null)),
            "job_requisition_status_no_change",
            ErrorType.Conflict);
        Assert.Equal(JobRequisitionStatus.Draft, requisition.JobRequisitionStatus);
        Assert.Equal(originalClosedAt, requisition.ClosedAt);
    }

    [Theory]
    [MemberData(nameof(InvalidTransitions))]
    public async Task ChangeStatus_AllInvalidTransitions_ReturnConflictWithoutMutation(
        JobRequisitionStatus current,
        JobRequisitionStatus next)
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        DateOnly? closedAt = current is JobRequisitionStatus.Closed or JobRequisitionStatus.Cancelled
            ? new DateOnly(2026, 2, 1)
            : null;
        var requisition = TestDatabase.JobRequisition(
            department, position, status: current, closedAt: closedAt);
        context.AddRange(department, position, requisition);
        await context.SaveChangesAsync();
        var requestedClosedAt = next is JobRequisitionStatus.Closed or JobRequisitionStatus.Cancelled
            ? new DateOnly(2026, 3, 1)
            : (DateOnly?)null;
        AssertError(
            await Service(context).ChangeStatusAsync(requisition.Id, new(next, requestedClosedAt)),
            "job_requisition_status_transition_invalid",
            ErrorType.Conflict);
        Assert.Equal(current, requisition.JobRequisitionStatus);
        Assert.Equal(closedAt, requisition.ClosedAt);
    }

    [Fact]
    public async Task ChangeStatus_MissingRequisition_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        AssertError(
            await Service(context).ChangeStatusAsync(
                Guid.NewGuid(), new(JobRequisitionStatus.Open, null)),
            "job_requisition_not_found",
            ErrorType.NotFound);
    }

    [Fact]
    public async Task ChangeStatus_InvalidEnum_ReturnsValidationWithoutMutation()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(department, position);
        context.AddRange(department, position, requisition);
        await context.SaveChangesAsync();

        AssertError(
            await Service(context).ChangeStatusAsync(
                requisition.Id, new((JobRequisitionStatus)999, null)),
            "job_requisition_status_invalid",
            ErrorType.Validation);
        Assert.Equal(JobRequisitionStatus.Draft, requisition.JobRequisitionStatus);
        Assert.Null(requisition.ClosedAt);
    }

    [Theory]
    [InlineData(JobRequisitionStatus.Closed)]
    [InlineData(JobRequisitionStatus.Cancelled)]
    public async Task ChangeStatus_TerminalTargetWithoutClosedAt_ReturnsValidationWithoutMutation(
        JobRequisitionStatus target)
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(
            department, position, status: JobRequisitionStatus.Open);
        context.AddRange(department, position, requisition);
        await context.SaveChangesAsync();

        AssertError(
            await Service(context).ChangeStatusAsync(requisition.Id, new(target, null)),
            "closed_at_required",
            ErrorType.Validation);
        Assert.Equal(JobRequisitionStatus.Open, requisition.JobRequisitionStatus);
        Assert.Null(requisition.ClosedAt);
    }

    [Theory]
    [InlineData(JobRequisitionStatus.Draft)]
    [InlineData(JobRequisitionStatus.Open)]
    [InlineData(JobRequisitionStatus.OnHold)]
    public async Task ChangeStatus_NonTerminalTargetWithClosedAt_ReturnsValidationWithoutMutation(
        JobRequisitionStatus target)
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(
            department, position, status: JobRequisitionStatus.Open);
        context.AddRange(department, position, requisition);
        await context.SaveChangesAsync();

        AssertError(
            await Service(context).ChangeStatusAsync(
                requisition.Id, new(target, new DateOnly(2026, 3, 1))),
            "closed_at_not_allowed",
            ErrorType.Validation);
        Assert.Equal(JobRequisitionStatus.Open, requisition.JobRequisitionStatus);
        Assert.Null(requisition.ClosedAt);
    }

    [Fact]
    public async Task ChangeStatus_ClosedDateBeforeOpenedDate_ReturnsValidation()
    {
        await using var context = TestDatabase.CreateContext();
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        var requisition = TestDatabase.JobRequisition(
            department, position, status: JobRequisitionStatus.Open, openedAt: new(2026, 2, 1));
        context.AddRange(department, position, requisition);
        await context.SaveChangesAsync();
        AssertError(
            await Service(context).ChangeStatusAsync(
                requisition.Id, new(JobRequisitionStatus.Closed, new(2026, 1, 1))),
            "closed_at_before_opened_at",
            ErrorType.Validation);
        Assert.Equal(JobRequisitionStatus.Open, requisition.JobRequisitionStatus);
        Assert.Null(requisition.ClosedAt);
    }

    public static TheoryData<JobRequisitionStatus, JobRequisitionStatus, DateOnly?>
        ValidTransitions => new()
        {
            { JobRequisitionStatus.Draft, JobRequisitionStatus.Open, null },
            { JobRequisitionStatus.Draft, JobRequisitionStatus.Cancelled, new(2026, 3, 1) },
            { JobRequisitionStatus.Open, JobRequisitionStatus.OnHold, null },
            { JobRequisitionStatus.Open, JobRequisitionStatus.Closed, new(2026, 3, 1) },
            { JobRequisitionStatus.Open, JobRequisitionStatus.Cancelled, new(2026, 3, 1) },
            { JobRequisitionStatus.OnHold, JobRequisitionStatus.Open, null },
            { JobRequisitionStatus.OnHold, JobRequisitionStatus.Closed, new(2026, 3, 1) },
            { JobRequisitionStatus.OnHold, JobRequisitionStatus.Cancelled, new(2026, 3, 1) }
        };

    public static TheoryData<JobRequisitionStatus, JobRequisitionStatus>
        InvalidTransitions => new()
        {
            { JobRequisitionStatus.Draft, JobRequisitionStatus.OnHold },
            { JobRequisitionStatus.Draft, JobRequisitionStatus.Closed },
            { JobRequisitionStatus.Open, JobRequisitionStatus.Draft },
            { JobRequisitionStatus.OnHold, JobRequisitionStatus.Draft },
            { JobRequisitionStatus.Closed, JobRequisitionStatus.Draft },
            { JobRequisitionStatus.Closed, JobRequisitionStatus.Open },
            { JobRequisitionStatus.Closed, JobRequisitionStatus.OnHold },
            { JobRequisitionStatus.Closed, JobRequisitionStatus.Cancelled },
            { JobRequisitionStatus.Cancelled, JobRequisitionStatus.Draft },
            { JobRequisitionStatus.Cancelled, JobRequisitionStatus.Open },
            { JobRequisitionStatus.Cancelled, JobRequisitionStatus.OnHold },
            { JobRequisitionStatus.Cancelled, JobRequisitionStatus.Closed }
        };

    private static void AddEvaluationCase(
        HrDecisionSupportDbContext context,
        JobRequisition requisition,
        string suffix)
    {
        var person = TestDatabase.Person($"REQ-{suffix}");
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            CandidateCode = $"CAN-{suffix}",
            CandidateSource = CandidateSource.Other
        };
        var evaluation = new CandidateEvaluationCase
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            JobRequisitionId = requisition.Id,
            ReceivedAtUtc = FixedUtc.UtcDateTime,
            Status = CandidateEvaluationStatus.New,
            CreatedAtUtc = FixedUtc.UtcDateTime
        };
        context.AddRange(person, candidate, evaluation);
    }

    private static JobRequisitionService Service(
        HrDecisionSupportDbContext context,
        TimeProvider? timeProvider = null) =>
        new(
            context,
            new CreateJobRequisitionRequestValidator(),
            new UpdateJobRequisitionRequestValidator(),
            new ChangeJobRequisitionStatusRequestValidator(),
            timeProvider ?? new FixedTimeProvider(FixedUtc));

    private static CreateJobRequisitionRequest ValidCreate(Guid departmentId, Guid positionId) =>
        new("REQ-NEW", "Engineer", departmentId, positionId, null, 1, null, new(2026, 1, 1), null);

    private static UpdateJobRequisitionRequest ValidUpdate(Guid departmentId, Guid positionId) =>
        new("REQ-UPDATED", "Engineer", departmentId, positionId, null, 1, null, new(2026, 1, 1));

    private static void AssertError(Result result, string code, ErrorType type)
    {
        Assert.True(result.IsFailure);
        Assert.Equal(code, Assert.Single(result.Errors).Code);
        Assert.Equal(type, result.Error!.Type);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
