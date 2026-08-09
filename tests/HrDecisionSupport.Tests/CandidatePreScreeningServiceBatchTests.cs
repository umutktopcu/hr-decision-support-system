using HrDecisionSupport.Application.PreScreening;
using HrDecisionSupport.Application.PreScreening.Models;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace HrDecisionSupport.Tests;

public class CandidatePreScreeningServiceBatchTests : IDisposable
{
    private readonly HrDecisionSupportDbContext _dbContext;
    private readonly CandidatePreScreeningService _service;

    public CandidatePreScreeningServiceBatchTests()
    {
        var options = new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new HrDecisionSupportDbContext(options);
        _service = new CandidatePreScreeningService(_dbContext);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private async Task<JobRequisition> CreateTestJobRequisitionAsync(string code = "BACKEND_DEVELOPER", int minExp = 24)
    {
        var position = new Position { Id = Guid.NewGuid(), Code = code, Name = "Test Position", IsActive = true };
        _dbContext.Positions.Add(position);

        var dept = new Department { Id = Guid.NewGuid(), Code = "IT", Name = "IT", IsActive = true };
        _dbContext.Departments.Add(dept);

        var comp1 = new Competency { Id = Guid.NewGuid(), Code = "C1", Name = "C1", IsActive = true };
        var comp2 = new Competency { Id = Guid.NewGuid(), Code = "C2", Name = "C2", IsActive = true };
        _dbContext.Competencies.AddRange(comp1, comp2);

        var job = new JobRequisition
        {
            Id = Guid.NewGuid(),
            RequisitionCode = "TEST-JOB",
            Title = "Test",
            DepartmentId = dept.Id,
            PositionId = position.Id,
            MinimumRelevantExperienceMonths = minExp,
            MandatorySkillCoverageThreshold = 0.50m,
            JobRequisitionStatus = JobRequisitionStatus.Open,
            OpenedAt = DateOnly.FromDateTime(DateTime.Today),
            CreatedAtUtc = DateTime.UtcNow
        };
        _dbContext.JobRequisitions.Add(job);

        job.Requirements.Add(new JobRequisitionRequirement { Id = Guid.NewGuid(), JobRequisitionId = job.Id, CompetencyId = comp1.Id, IsRequired = true });
        job.Requirements.Add(new JobRequisitionRequirement { Id = Guid.NewGuid(), JobRequisitionId = job.Id, CompetencyId = comp2.Id, IsRequired = false });

        await _dbContext.SaveChangesAsync();
        return job;
    }

    private async Task<CandidateEvaluationCase> AddCandidateAsync(JobRequisition job, bool isEligible, bool duplicate = false, Guid? specificCandidateId = null)
    {
        var comp1 = await _dbContext.Competencies.FirstAsync(c => c.Code == "C1");

        var person = new Person { Id = Guid.NewGuid(), AnonymousCode = "CAND", CreatedAtUtc = DateTime.UtcNow };
        if (isEligible)
        {
            person.PersonCompetencies.Add(new PersonCompetency { Id = Guid.NewGuid(), PersonId = person.Id, CompetencyId = comp1.Id });
        }

        var candidateId = specificCandidateId ?? Guid.NewGuid();
        var candidate = await _dbContext.Candidates.FindAsync(candidateId);

        if (candidate == null)
        {
            candidate = new Candidate { Id = candidateId, CandidateCode = "CAND-" + candidateId.ToString()[..8], PersonId = person.Id, Person = person };

            if (isEligible)
            {
                candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot
                {
                    Id = Guid.NewGuid(),
                    CandidateId = candidate.Id,
                    BackendExperienceMonths = 36,
                    FeatureSchemaVersion = "v1",
                    CalculatedAtUtc = DateTime.UtcNow
                });
            }

            _dbContext.Candidates.Add(candidate);
        }

        var evalCase = new CandidateEvaluationCase
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            JobRequisitionId = job.Id,
            ReceivedAtUtc = DateTime.UtcNow,
            CreatedAtUtc = DateTime.UtcNow,
            Status = CandidateEvaluationStatus.New,
            Candidate = candidate,
            JobRequisition = job
        };

        _dbContext.CandidateEvaluationCases.Add(evalCase);
        await _dbContext.SaveChangesAsync();

        return evalCase;
    }

    [Fact]
    public async Task EvaluateApplicantsForJobAsync_3Candidates_2Eligible1Rejected_ReturnsCorrectCounts()
    {
        var job = await CreateTestJobRequisitionAsync();

        await AddCandidateAsync(job, isEligible: true);
        await AddCandidateAsync(job, isEligible: true);
        await AddCandidateAsync(job, isEligible: false);

        var result = await _service.EvaluateApplicantsForJobAsync(job.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalApplicants);
        Assert.Equal(2, result.Value.EligibleCount);
        Assert.Equal(1, result.Value.RejectedCount);
    }

    [Fact]
    public async Task EvaluateApplicantsForJobAsync_CandidateOnAnotherJob_DoesNotEnterBatch()
    {
        var job1 = await CreateTestJobRequisitionAsync();
        var job2 = await CreateTestJobRequisitionAsync();

        await AddCandidateAsync(job1, isEligible: true);
        await AddCandidateAsync(job2, isEligible: true);

        var result = await _service.EvaluateApplicantsForJobAsync(job1.Id);

        Assert.Equal(1, result.Value.TotalApplicants);
    }

    [Fact]
    public async Task EvaluateApplicantsForJobAsync_CandidateWithNoCase_DoesNotEnterBatch()
    {
        var job = await CreateTestJobRequisitionAsync();

        var person = new Person { Id = Guid.NewGuid(), AnonymousCode = "NOCASE", CreatedAtUtc = DateTime.UtcNow };
        var candidate = new Candidate { Id = Guid.NewGuid(), CandidateCode = "CAND-NO-CASE", PersonId = person.Id, Person = person };
        _dbContext.Candidates.Add(candidate);
        await _dbContext.SaveChangesAsync();

        var result = await _service.EvaluateApplicantsForJobAsync(job.Id);

        Assert.Equal(0, result.Value.TotalApplicants);
    }

    [Fact]
    public async Task EvaluateApplicantsForJobAsync_DuplicateCandidateJobCase_EvaluatesOnce()
    {
        var job = await CreateTestJobRequisitionAsync();

        var case1 = await AddCandidateAsync(job, isEligible: true);
        var case2 = await AddCandidateAsync(job, isEligible: true, duplicate: true, specificCandidateId: case1.CandidateId);

        var result = await _service.EvaluateApplicantsForJobAsync(job.Id);

        Assert.Equal(1, result.Value.TotalApplicants);
        Assert.Equal(1, result.Value.EligibleCount);
    }

    [Fact]
    public async Task EvaluateApplicantsForJobAsync_EligibleResults_ContainsOnlyEligible()
    {
        var job = await CreateTestJobRequisitionAsync();

        await AddCandidateAsync(job, isEligible: true);
        await AddCandidateAsync(job, isEligible: false);

        var result = await _service.EvaluateApplicantsForJobAsync(job.Id);

        Assert.Single(result.Value.EligibleResults);
        Assert.True(result.Value.EligibleResults.All(r => r.EligibleForSemanticEvaluation));
    }

    [Fact]
    public async Task EvaluateApplicantsForJobAsync_BatchResultSameAsSingleResult()
    {
        var job = await CreateTestJobRequisitionAsync();
        var evalCase = await AddCandidateAsync(job, isEligible: true);

        var batchResult = await _service.EvaluateApplicantsForJobAsync(job.Id);
        var singleResult = await _service.EvaluateAsync(evalCase.Id);

        var batchCandidateResult = batchResult.Value.Results.First();

        Assert.Equal(singleResult.Value.EligibleForSemanticEvaluation, batchCandidateResult.EligibleForSemanticEvaluation);
        Assert.Equal(singleResult.Value.FailureReasons, batchCandidateResult.FailureReasons);
    }

    [Fact]
    public async Task EvaluateApplicantsForJobAsync_FailureReasonsPreserved()
    {
        var job = await CreateTestJobRequisitionAsync();
        var evalCase = await AddCandidateAsync(job, isEligible: false); // Will fail mandatory skill and experience

        var batchResult = await _service.EvaluateApplicantsForJobAsync(job.Id);

        var result = batchResult.Value.Results.First();
        Assert.NotEmpty(result.FailureReasons);
        Assert.Contains(PreScreeningFailureReason.MandatorySkillCoverageBelowThreshold, result.FailureReasons);
    }

    [Fact]
    public async Task EvaluateApplicantsForJobAsync_EmptyApplicantPool_ReturnsCleanEmptyResult()
    {
        var job = await CreateTestJobRequisitionAsync();

        var result = await _service.EvaluateApplicantsForJobAsync(job.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.Value.TotalApplicants);
        Assert.Equal(0, result.Value.EligibleCount);
        Assert.Equal(0, result.Value.RejectedCount);
        Assert.Empty(result.Value.Results);
        Assert.Empty(result.Value.EligibleResults);
    }

    [Fact]
    public async Task EvaluateApplicantsForJobAsync_JobNotFound_ReturnsFailure()
    {
        var result = await _service.EvaluateApplicantsForJobAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("job_requisition_not_found", result.Errors.First().Code);
    }
}
