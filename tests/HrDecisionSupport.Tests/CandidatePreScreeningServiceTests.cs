using HrDecisionSupport.Application.PreScreening;
using HrDecisionSupport.Application.PreScreening.Models;
using HrDecisionSupport.Application.PreScreening.Policy;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using HrDecisionSupport.Infrastructure.Persistence;

namespace HrDecisionSupport.Tests;

public class CandidatePreScreeningServiceTests
{
    private static HrDecisionSupportDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new HrDecisionSupportDbContext(options);
    }

    [Fact]
    public async Task EvaluateAsync_MissingEvaluationCase_ReturnsFailure()
    {
        var db = CreateInMemoryContext();
        var service = new CandidatePreScreeningService(db, new CandidatePreScreeningPolicy());

        var result = await service.EvaluateAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("evaluation_case_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task EvaluateAsync_BackendRole_WithExperienceRequirement_ValidSnapshot_Passes()
    {
        var db = CreateInMemoryContext();

        var comp1 = new Competency { Id = Guid.NewGuid(), Code = "C1", Name = "C1", IsActive = true };
        var comp2 = new Competency { Id = Guid.NewGuid(), Code = "C2", Name = "C2", IsActive = true };

        db.Competencies.AddRange(comp1, comp2);

        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            CandidateCode = "C1",
            Person = new Person
            {
                Id = Guid.NewGuid(),
                AnonymousCode = "A",
                FirstName = "Test",
                LastName = "User",
                PersonCompetencies = new List<PersonCompetency>
                {
                    new() { CompetencyId = comp1.Id, Id = Guid.NewGuid() },
                    new() { CompetencyId = comp2.Id, Id = Guid.NewGuid() }
                }
            }
        };

        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            FeatureSchemaVersion = "1.0",
            CalculatedAtUtc = DateTime.UtcNow,
            BackendExperienceMonths = 40 // > 36
        });

        var position = new Position { Id = Guid.NewGuid(), Code = "BACKEND_DEVELOPER", Name = "Backend", IsActive = true };
        var department = new Department { Id = Guid.NewGuid(), Code = "IT", Name = "IT", IsActive = true };

        var job = new JobRequisition
        {
            Id = Guid.NewGuid(),
            RequisitionCode = "R1",
            Title = "Backend Dev",
            DepartmentId = department.Id,
            PositionId = position.Id,
            OpenedAt = new(2026, 1, 1),
            MinimumRelevantExperienceMonths = 36,
            Requirements = new List<JobRequisitionRequirement>
            {
                new() { Id = Guid.NewGuid(), CompetencyId = comp1.Id, IsRequired = true },
                new() { Id = Guid.NewGuid(), CompetencyId = comp2.Id, IsRequired = false }
            }
        };

        var evalCase = new CandidateEvaluationCase
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            JobRequisitionId = job.Id,
            Candidate = candidate,
            JobRequisition = job
        };

        db.Positions.Add(position);
        db.Departments.Add(department);
        db.Candidates.Add(candidate);
        db.JobRequisitions.Add(job);
        db.CandidateEvaluationCases.Add(evalCase);

        await db.SaveChangesAsync();

        var service = new CandidatePreScreeningService(db, new CandidatePreScreeningPolicy());
        var result = await service.EvaluateAsync(evalCase.Id);

        Assert.True(result.IsSuccess);

        var payload = result.Value;
        Assert.True(payload.EligibleForSemanticEvaluation);
        Assert.Empty(payload.FailureReasons);

        Assert.True(payload.MandatorySkillResult.Passed);
        Assert.True(payload.PreferredSkillResult.Passed);
        Assert.True(payload.ExperienceResult.Passed);
    }

    [Fact]
    public async Task EvaluateAsync_BackendRole_NoSnapshot_MissingExperienceData()
    {
        var db = CreateInMemoryContext();

        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            CandidateCode = "C1",
            Person = new Person { Id = Guid.NewGuid(), AnonymousCode = "A", FirstName = "Test", LastName = "User" }
        };

        var position = new Position { Id = Guid.NewGuid(), Code = "BACKEND_DEVELOPER", Name = "Backend", IsActive = true };
        var department = new Department { Id = Guid.NewGuid(), Code = "IT", Name = "IT", IsActive = true };

        var job = new JobRequisition
        {
            Id = Guid.NewGuid(),
            RequisitionCode = "R1",
            Title = "Backend Dev",
            DepartmentId = department.Id,
            PositionId = position.Id,
            OpenedAt = new(2026, 1, 1),
            MinimumRelevantExperienceMonths = 36
        };

        var evalCase = new CandidateEvaluationCase
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            JobRequisitionId = job.Id,
            Candidate = candidate,
            JobRequisition = job
        };

        db.Positions.Add(position);
        db.Departments.Add(department);
        db.Candidates.Add(candidate);
        db.JobRequisitions.Add(job);
        db.CandidateEvaluationCases.Add(evalCase);

        await db.SaveChangesAsync();

        var service = new CandidatePreScreeningService(db, new CandidatePreScreeningPolicy());
        var result = await service.EvaluateAsync(evalCase.Id);

        Assert.True(result.IsSuccess);

        var payload = result.Value;
        Assert.False(payload.EligibleForSemanticEvaluation);
        Assert.Contains(PreScreeningFailureReason.MissingRelevantExperienceData, payload.FailureReasons);
    }

    [Fact]
    public async Task EvaluateAsync_NonBackendRole_ExperienceRequirement_UnsupportedSource()
    {
        var db = CreateInMemoryContext();

        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            CandidateCode = "C1",
            Person = new Person { Id = Guid.NewGuid(), AnonymousCode = "A", FirstName = "Test", LastName = "User" }
        };

        var position = new Position { Id = Guid.NewGuid(), Code = "FRONTEND_DEVELOPER", Name = "Frontend", IsActive = true };
        var department = new Department { Id = Guid.NewGuid(), Code = "IT", Name = "IT", IsActive = true };

        var job = new JobRequisition
        {
            Id = Guid.NewGuid(),
            RequisitionCode = "R1",
            Title = "Frontend Dev",
            DepartmentId = department.Id,
            PositionId = position.Id,
            OpenedAt = new(2026, 1, 1),
            MinimumRelevantExperienceMonths = 36
        };

        var evalCase = new CandidateEvaluationCase
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            JobRequisitionId = job.Id,
            Candidate = candidate,
            JobRequisition = job
        };

        db.Positions.Add(position);
        db.Departments.Add(department);
        db.Candidates.Add(candidate);
        db.JobRequisitions.Add(job);
        db.CandidateEvaluationCases.Add(evalCase);

        await db.SaveChangesAsync();

        var service = new CandidatePreScreeningService(db, new CandidatePreScreeningPolicy());
        var result = await service.EvaluateAsync(evalCase.Id);

        Assert.True(result.IsSuccess);

        var payload = result.Value;
        Assert.False(payload.EligibleForSemanticEvaluation);
        Assert.Contains(PreScreeningFailureReason.UnsupportedRelevantExperienceSource, payload.FailureReasons);
    }

    [Fact]
    public async Task EvaluateAsync_VersionSorting_SelectsCorrectSnapshot()
    {
        var db = CreateInMemoryContext();

        var candidate = new Candidate
        {
            Id = Guid.NewGuid(),
            CandidateCode = "C_VS",
            Person = new Person { Id = Guid.NewGuid(), AnonymousCode = "A_VS", FirstName = "Test", LastName = "User" }
        };

        var dateOld = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var dateNew = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // v2 vs v10 -> v10 selected
        // v10 old vs v9 new -> v10 selected
        // v10 old vs v10 new -> v10 new selected
        // invalid version -> deterministic fallback
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot { Id = Guid.NewGuid(), CandidateId = candidate.Id, FeatureSchemaVersion = "2.0", CalculatedAtUtc = dateNew, BackendExperienceMonths = 20 });
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot { Id = Guid.NewGuid(), CandidateId = candidate.Id, FeatureSchemaVersion = "9.0", CalculatedAtUtc = dateNew, BackendExperienceMonths = 90 });
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot { Id = Guid.NewGuid(), CandidateId = candidate.Id, FeatureSchemaVersion = "10.0", CalculatedAtUtc = dateOld, BackendExperienceMonths = 100 });
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot { Id = Guid.NewGuid(), CandidateId = candidate.Id, FeatureSchemaVersion = "10.0", CalculatedAtUtc = dateNew, BackendExperienceMonths = 101 }); // This should win
        candidate.CareerFeatureSnapshots.Add(new CandidateCareerFeatureSnapshot { Id = Guid.NewGuid(), CandidateId = candidate.Id, FeatureSchemaVersion = "invalid", CalculatedAtUtc = dateNew, BackendExperienceMonths = 999 });

        var position = new Position { Id = Guid.NewGuid(), Code = "BACKEND_DEVELOPER", Name = "Backend", IsActive = true };
        var department = new Department { Id = Guid.NewGuid(), Code = "IT", Name = "IT", IsActive = true };

        var job = new JobRequisition
        {
            Id = Guid.NewGuid(),
            RequisitionCode = "R1",
            Title = "Backend Dev",
            DepartmentId = department.Id,
            PositionId = position.Id,
            OpenedAt = new(2026, 1, 1),
            MinimumRelevantExperienceMonths = 50 // We expect 101
        };

        var evalCase = new CandidateEvaluationCase
        {
            Id = Guid.NewGuid(),
            CandidateId = candidate.Id,
            JobRequisitionId = job.Id,
            Candidate = candidate,
            JobRequisition = job
        };

        db.Positions.Add(position);
        db.Departments.Add(department);
        db.Candidates.Add(candidate);
        db.JobRequisitions.Add(job);
        db.CandidateEvaluationCases.Add(evalCase);

        await db.SaveChangesAsync();

        var service = new CandidatePreScreeningService(db, new CandidatePreScreeningPolicy());
        var result = await service.EvaluateAsync(evalCase.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.ExperienceResult.Passed);
        Assert.Equal(101, result.Value.ExperienceResult.CandidateRelevantExperienceMonths);
    }
}
