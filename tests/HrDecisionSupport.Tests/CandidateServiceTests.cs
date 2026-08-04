using HrDecisionSupport.Application.Candidates;
using HrDecisionSupport.Application.Candidates.Dtos;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

public class CandidateServiceTests
{
    [Fact]
    public async Task ListAsync_EmptyDatabase_ReturnsSuccessfulEmptyList()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await CreateService(context).ListAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ListAsync_OrdersByCandidateCode()
    {
        await using var context = TestDatabase.CreateContext();
        AddCandidate(context, "CAN-20", "ANON-20");
        AddCandidate(context, "CAN-01", "ANON-01");
        await context.SaveChangesAsync();

        var result = await CreateService(context).ListAsync();

        Assert.Equal(["CAN-01", "CAN-20"], result.Value.Select(item => item.CandidateCode));
    }

    [Fact]
    public async Task GetByIdAsync_MissingCandidate_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await CreateService(context).GetByIdAsync(Guid.NewGuid());

        AssertFailure(result, "candidate_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsEvaluationCaseCount()
    {
        await using var context = TestDatabase.CreateContext();
        var candidate = AddCandidate(context, "CAN-01", "ANON-01");
        context.CandidateEvaluationCases.AddRange(
            EvaluationCase(candidate),
            EvaluationCase(candidate));
        await context.SaveChangesAsync();

        var result = await CreateService(context).GetByIdAsync(candidate.Id);

        Assert.Equal(2, result.Value.EvaluationCaseCount);
    }

    [Fact]
    public async Task CreateAsync_ValidRequest_CreatesPersonAndCandidate()
    {
        await using var context = TestDatabase.CreateContext();

        var result = await CreateService(context).CreateAsync(CreateRequest() with
        {
            CandidateCode = " CAN-01 ",
            AnonymousCode = " ANON-01 ",
            ExternalCandidateId = " EXT-42 "
        });

        Assert.True(result.IsSuccess);
        Assert.Equal("CAN-01", result.Value.CandidateCode);
        Assert.Equal("ANON-01", result.Value.AnonymousCode);
        Assert.Equal("EXT-42", result.Value.ExternalCandidateId);
        Assert.Equal(1, await context.People.CountAsync());
        Assert.Equal(1, await context.Candidates.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_DuplicateCandidateCode_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        AddCandidate(context, "CAN-01", "OTHER");
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest());

        AssertFailure(result, "candidate_code_conflict", ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateAsync_ExistingEmployeePerson_ReusesPerson()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("ANON-01");
        context.People.Add(person);
        context.Employees.Add(new Employee
        {
            Id = Guid.NewGuid(), PersonId = person.Id, Person = person,
            EmployeeCode = "EMP-01", HireDate = new DateOnly(2024, 1, 1),
            EmploymentStatus = EmploymentStatus.Active
        });
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(person.Id, result.Value.PersonId);
        Assert.Equal(1, await context.People.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_ExistingRolelessPerson_ReusesPersonWithoutCreatingSecondPerson()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("ANON-01");
        context.People.Add(person);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Equal(person.Id, result.Value.PersonId);
        Assert.Equal(1, await context.People.CountAsync());
        Assert.Equal(person.Id, (await context.Candidates.SingleAsync()).PersonId);
    }

    [Fact]
    public async Task CreateAsync_ExistingPersonWithNullFields_CompletesMissingPersonData()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person(
            "ANON-01",
            firstName: null,
            lastName: null,
            email: null,
            phoneNumber: null);
        context.People.Add(person);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        var persisted = await context.People.SingleAsync();
        Assert.Equal("Ada", persisted.FirstName);
        Assert.Equal("Lovelace", persisted.LastName);
        Assert.Equal("ada@example.com", persisted.Email);
        Assert.Equal("+90-555-000-0000", persisted.PhoneNumber);
        Assert.NotNull(persisted.UpdatedAtUtc);
    }

    [Fact]
    public async Task CreateAsync_ExistingEmailDiffersOnlyByCase_DoesNotReturnPersonDataConflict()
    {
        await using var context = TestDatabase.CreateContext();
        var person = TestDatabase.Person("ANON-01", email: "ADA@EXAMPLE.COM");
        context.People.Add(person);
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Errors, error => error.Code == "person_data_conflict");
        Assert.Equal(person.Id, result.Value.PersonId);
        Assert.Equal(1, await context.People.CountAsync());
    }

    [Fact]
    public async Task CreateAsync_PersonAlreadyCandidate_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        AddCandidate(context, "OTHER", "ANON-01");
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest());

        AssertFailure(result, "person_candidate_role_conflict", ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateAsync_ConflictingPersonData_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        context.People.Add(TestDatabase.Person("ANON-01", email: "other@example.com"));
        await context.SaveChangesAsync();

        var result = await CreateService(context).CreateAsync(CreateRequest());

        AssertFailure(result, "person_data_conflict", ErrorType.Conflict);
    }

    [Fact]
    public async Task CreateAsync_InvalidCandidateSource_ReturnsValidationFailure()
    {
        await using var context = TestDatabase.CreateContext();

        var result = await CreateService(context).CreateAsync(
            CreateRequest() with { CandidateSource = (CandidateSource)12345 });

        AssertFailure(result, "candidate_source_invalid", ErrorType.Validation);
    }

    [Fact]
    public async Task ServiceResults_DoNotExposeCandidateLevelStatus()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await CreateService(context).CreateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.GetType().GetProperty("Status"));
        Assert.Null(typeof(CandidateListItemDto).GetProperty("Status"));
    }

    [Fact]
    public async Task UpdateAsync_MissingCandidate_ReturnsNotFound()
    {
        await using var context = TestDatabase.CreateContext();
        var result = await CreateService(context).UpdateAsync(Guid.NewGuid(), UpdateRequest());

        AssertFailure(result, "candidate_not_found", ErrorType.NotFound);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateCandidateCode_ReturnsConflict()
    {
        await using var context = TestDatabase.CreateContext();
        var candidate = AddCandidate(context, "CAN-01", "ANON-01");
        AddCandidate(context, "CAN-02", "ANON-02");
        await context.SaveChangesAsync();

        var result = await CreateService(context).UpdateAsync(
            candidate.Id,
            UpdateRequest() with { CandidateCode = "CAN-02" });

        AssertFailure(result, "candidate_code_conflict", ErrorType.Conflict);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCandidateAndPersonFields()
    {
        await using var context = TestDatabase.CreateContext();
        var candidate = AddCandidate(context, "CAN-01", "ANON-01");
        await context.SaveChangesAsync();
        var request = UpdateRequest() with
        {
            CandidateCode = " CAN-NEW ",
            FirstName = "Grace",
            Email = "grace@example.com",
            CandidateSource = CandidateSource.Ats,
            ExternalCandidateId = " ATS-42 "
        };

        var result = await CreateService(context).UpdateAsync(candidate.Id, request);

        Assert.True(result.IsSuccess);
        Assert.Equal("CAN-NEW", result.Value.CandidateCode);
        Assert.Equal("Grace", result.Value.FirstName);
        Assert.Equal("grace@example.com", result.Value.Email);
        Assert.Equal(CandidateSource.Ats, result.Value.CandidateSource);
        Assert.Equal("ATS-42", result.Value.ExternalCandidateId);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotChangeEvaluationCases()
    {
        await using var context = TestDatabase.CreateContext();
        var candidate = AddCandidate(context, "CAN-01", "ANON-01");
        var evaluationCase = EvaluationCase(candidate);
        context.CandidateEvaluationCases.Add(evaluationCase);
        await context.SaveChangesAsync();
        var original = (
            evaluationCase.JobRequisitionId,
            evaluationCase.Status,
            evaluationCase.ReceivedAtUtc,
            evaluationCase.Notes);

        await CreateService(context).UpdateAsync(candidate.Id, UpdateRequest());

        var persisted = await context.CandidateEvaluationCases.SingleAsync();
        Assert.Equal(
            original,
            (persisted.JobRequisitionId, persisted.Status, persisted.ReceivedAtUtc, persisted.Notes));
    }

    private static CandidateService CreateService(HrDecisionSupportDbContext context) =>
        new(context, new CreateCandidateRequestValidator(), new UpdateCandidateRequestValidator());

    private static Candidate AddCandidate(
        HrDecisionSupportDbContext context,
        string candidateCode,
        string anonymousCode)
    {
        var person = TestDatabase.Person(anonymousCode);
        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            Person = person,
            CandidateCode = candidateCode,
            CandidateSource = CandidateSource.Referral
        };
        context.People.Add(person);
        context.Candidates.Add(candidate);
        return candidate;
    }

    private static CandidateEvaluationCase EvaluationCase(Candidate candidate) =>
        new()
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            Candidate = candidate,
            JobRequisitionId = Guid.NewGuid(),
            ReceivedAtUtc = DateTime.UtcNow,
            Status = CandidateEvaluationStatus.InReview,
            Notes = "Unchanged",
            CreatedAtUtc = DateTime.UtcNow
        };

    private static CreateCandidateRequest CreateRequest() =>
        new(
            "CAN-01",
            "ANON-01",
            "Ada",
            "Lovelace",
            "ada@example.com",
            "+90-555-000-0000",
            CandidateSource.Referral,
            null);

    private static UpdateCandidateRequest UpdateRequest() =>
        new(
            "CAN-01",
            "Ada",
            "Lovelace",
            "ada@example.com",
            "+90-555-000-0000",
            CandidateSource.Referral,
            null);

    private static void AssertFailure<T>(Result<T> result, string code, ErrorType type)
    {
        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == code && error.Type == type);
    }
}
