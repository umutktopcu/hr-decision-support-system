using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.PreScreening;
using HrDecisionSupport.Application.PreScreening.Models;
using HrDecisionSupport.Application.SemanticMatching.Documents;
using HrDecisionSupport.Application.SemanticMatching.Orchestration;
using HrDecisionSupport.Application.SemanticMatching.Orchestration.Models;
using HrDecisionSupport.Application.SemanticMatching.Retrieval;
using HrDecisionSupport.Application.SemanticMatching.Retrieval.Models;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Tests;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace HrDecisionSupport.Tests.Application.SemanticMatching.Orchestration;

public class CandidateSemanticMatchingServiceTests
{
    private readonly Mock<ICandidatePreScreeningService> _preScreeningMock;
    private readonly Mock<ISemanticRetrievalService> _retrievalMock;
    private readonly CandidateDocumentBuilder _candidateDocumentBuilder;
    private readonly JobDocumentBuilder _jobDocumentBuilder;

    public CandidateSemanticMatchingServiceTests()
    {
        _preScreeningMock = new Mock<ICandidatePreScreeningService>();
        _retrievalMock = new Mock<ISemanticRetrievalService>();
        _candidateDocumentBuilder = new CandidateDocumentBuilder();
        _jobDocumentBuilder = new JobDocumentBuilder();
    }

    private CandidateSemanticMatchingService CreateService(Infrastructure.Persistence.HrDecisionSupportDbContext dbContext)
    {
        return new CandidateSemanticMatchingService(
            dbContext,
            _preScreeningMock.Object,
            _retrievalMock.Object,
            _candidateDocumentBuilder,
            _jobDocumentBuilder);
    }

    [Fact]
    public async Task MatchApplicantsForJobAsync_InvalidTopN_ReturnsFailure()
    {
        await using var context = TestDatabase.CreateContext();
        var sut = CreateService(context);
        var result = await sut.MatchApplicantsForJobAsync(Guid.NewGuid(), 0);

        Assert.True(result.IsFailure);
        Assert.Equal("invalid_topn", result.Error!.Code);
    }

    [Fact]
    public async Task MatchApplicantsForJobAsync_PreScreeningFails_PropagatesError()
    {
        await using var context = TestDatabase.CreateContext();
        var sut = CreateService(context);
        var jobId = Guid.NewGuid();
        _preScreeningMock.Setup(x => x.EvaluateApplicantsForJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidatePreScreeningBatchResult>.Failure(new Error("pre_screen_error", "Failed")));

        var result = await sut.MatchApplicantsForJobAsync(jobId, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("pre_screen_error", result.Error!.Code);
    }

    [Fact]
    public async Task MatchApplicantsForJobAsync_EmptyEligiblePool_ReturnsSuccessWithEmptyResults()
    {
        await using var context = TestDatabase.CreateContext();
        var sut = CreateService(context);
        var jobId = Guid.NewGuid();
        
        // Add job to DB so it doesn't fail on Job Not Found
        var dep = TestDatabase.Department();
        var pos = TestDatabase.Position();
        var job = TestDatabase.JobRequisition(dep, pos);
        job.Id = jobId;
        context.Departments.Add(dep);
        context.Positions.Add(pos);
        context.JobRequisitions.Add(job);
        await context.SaveChangesAsync();

        var batchResult = new CandidatePreScreeningBatchResult(jobId, 5, 0, 5, Array.Empty<CandidatePreScreeningResult>());
        _preScreeningMock.Setup(x => x.EvaluateApplicantsForJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidatePreScreeningBatchResult>.Success(batchResult));

        var result = await sut.MatchApplicantsForJobAsync(jobId, 10);

        Assert.True(result.IsSuccess);
        Assert.Equal(jobId, result.Value.JobRequisitionId);
        Assert.Equal(5, result.Value.TotalApplicants);
        Assert.Equal(0, result.Value.PreScreeningEligibleCount);
        Assert.Equal(5, result.Value.PreScreeningRejectedCount);
        Assert.Equal(0, result.Value.RetrievedCount);
        Assert.Empty(result.Value.Results);
        
        // Verify retrieval service was NEVER called
        _retrievalMock.Verify(x => x.RetrieveTopCandidatesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<SemanticCandidateDocument>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MatchApplicantsForJobAsync_JobNotFound_ReturnsFailure()
    {
        await using var context = TestDatabase.CreateContext();
        var sut = CreateService(context);
        var jobId = Guid.NewGuid();
        
        var batchResult = new CandidatePreScreeningBatchResult(jobId, 5, 1, 4, new[] { 
            CreateMockResult(Guid.NewGuid(), jobId, true)
        });
        
        _preScreeningMock.Setup(x => x.EvaluateApplicantsForJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidatePreScreeningBatchResult>.Success(batchResult));

        // DB is empty, so JobRequisition is missing

        var result = await sut.MatchApplicantsForJobAsync(jobId, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("job_requisition_not_found", result.Error!.Code);
    }

    [Fact]
    public async Task MatchApplicantsForJobAsync_CandidateMissingInDb_ReturnsIntegrityFailure()
    {
        await using var context = TestDatabase.CreateContext();
        var sut = CreateService(context);
        var jobId = Guid.NewGuid();
        
        var dep = TestDatabase.Department();
        var pos = TestDatabase.Position();
        var job = TestDatabase.JobRequisition(dep, pos);
        job.Id = jobId;
        context.Departments.Add(dep);
        context.Positions.Add(pos);
        context.JobRequisitions.Add(job);
        await context.SaveChangesAsync();

        var candidateId = Guid.NewGuid();
        var batchResult = new CandidatePreScreeningBatchResult(jobId, 1, 1, 0, new[] { 
            CreateMockResult(candidateId, jobId, true)
        });
        
        _preScreeningMock.Setup(x => x.EvaluateApplicantsForJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidatePreScreeningBatchResult>.Success(batchResult));

        // Note: We don't add the candidate to DbContext, simulating DB integrity issue

        var result = await sut.MatchApplicantsForJobAsync(jobId, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("candidate_data_integrity_error", result.Error!.Code);
    }

    [Fact]
    public async Task MatchApplicantsForJobAsync_RetrievalServiceFails_PropagatesError()
    {
        await using var context = TestDatabase.CreateContext();
        var sut = CreateService(context);
        var jobId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var dep = TestDatabase.Department();
        var pos = TestDatabase.Position();
        var job = TestDatabase.JobRequisition(dep, pos);
        job.Id = jobId;
        context.Departments.Add(dep);
        context.Positions.Add(pos);
        
        var person = TestDatabase.Person("ANON-01");
        var candidate = TestDatabase.Candidate(person);
        candidate.Id = candidateId;
        candidate.Person = person;
        
        context.JobRequisitions.Add(job);
        context.Candidates.Add(candidate);
        await context.SaveChangesAsync();

        var batchResult = new CandidatePreScreeningBatchResult(jobId, 1, 1, 0, new[] { 
            CreateMockResult(candidateId, jobId, true)
        });
        
        _preScreeningMock.Setup(x => x.EvaluateApplicantsForJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidatePreScreeningBatchResult>.Success(batchResult));

        _retrievalMock.Setup(x => x.RetrieveTopCandidatesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<SemanticCandidateDocument>>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<SemanticRetrievalResult>>.Failure(new Error("retrieval_error", "Failed")));

        var result = await sut.MatchApplicantsForJobAsync(jobId, 10);

        Assert.True(result.IsFailure);
        Assert.Equal("retrieval_error", result.Error!.Code);
    }

    [Fact]
    public async Task MatchApplicantsForJobAsync_ValidFlow_ReturnsCorrectResult()
    {
        await using var context = TestDatabase.CreateContext();
        var sut = CreateService(context);
        var jobId = Guid.NewGuid();
        var dep = TestDatabase.Department();
        var pos = TestDatabase.Position();
        var job = TestDatabase.JobRequisition(dep, pos);
        job.Id = jobId;
        context.Departments.Add(dep);
        context.Positions.Add(pos);
        
        var eligibleCandidateId = Guid.NewGuid();
        var rejectedCandidateId = Guid.NewGuid();
        var person1 = TestDatabase.Person("ANON-01");
        var candidate1 = TestDatabase.Candidate(person1);
        candidate1.Id = eligibleCandidateId;
        candidate1.ProfessionalTitle = "Title1";
        candidate1.Person = person1;
        
        var person2 = TestDatabase.Person("ANON-02");
        var candidate2 = TestDatabase.Candidate(person2);
        candidate2.Id = rejectedCandidateId;
        candidate2.ProfessionalTitle = "Title2";
        candidate2.Person = person2;

        context.JobRequisitions.Add(job);
        context.Candidates.Add(candidate1);
        context.Candidates.Add(candidate2);
        await context.SaveChangesAsync();

        var batchResult = new CandidatePreScreeningBatchResult(jobId, 2, 1, 1, new[] { 
            CreateMockResult(eligibleCandidateId, jobId, true),
            CreateMockResult(rejectedCandidateId, jobId, false)
        });
        
        _preScreeningMock.Setup(x => x.EvaluateApplicantsForJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidatePreScreeningBatchResult>.Success(batchResult));

        var expectedRetrievalResult = new List<SemanticRetrievalResult>
        {
            new SemanticRetrievalResult(eligibleCandidateId, 0.95, "CANDIDATE_DOC")
        };

        _retrievalMock.Setup(x => x.RetrieveTopCandidatesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<SemanticCandidateDocument>>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<SemanticRetrievalResult>>.Success(expectedRetrievalResult))
            .Callback<string, IReadOnlyList<SemanticCandidateDocument>, int, CancellationToken>((jDoc, cDocs, n, c) => 
            {
                // Verify ONLY eligible candidate was sent
                Assert.Single(cDocs);
                Assert.Equal(eligibleCandidateId, cDocs[0].CandidateId);
                // Verify document text was generated correctly (ProfessionalTitle mapped in CandidateDocumentBuilder)
                Assert.Contains("Title1", cDocs[0].Text);
            });

        var result = await sut.MatchApplicantsForJobAsync(jobId, 5);

        Assert.True(result.IsSuccess);
        Assert.Equal(jobId, result.Value.JobRequisitionId);
        Assert.Equal(2, result.Value.TotalApplicants);
        Assert.Equal(1, result.Value.PreScreeningEligibleCount);
        Assert.Equal(1, result.Value.PreScreeningRejectedCount);
        Assert.Equal(1, result.Value.RetrievedCount);
        Assert.Equal(5, result.Value.RequestedTopN);
        Assert.Single(result.Value.Results);
        Assert.Equal(eligibleCandidateId, result.Value.Results[0].CandidateId);
    }
    
    [Fact]
    public async Task MatchApplicantsForJobAsync_LoadsRequiredCandidateNavigationProperties()
    {
        // This test ensures the correct EF Includes are being triggered by checking that
        // the candidate passed to the Builder has its nested collections.
        await using var context = TestDatabase.CreateContext();
        var sut = CreateService(context);
        var jobId = Guid.NewGuid();
        var candidateId = Guid.NewGuid();
        var dep = TestDatabase.Department();
        var pos = TestDatabase.Position();
        var job = TestDatabase.JobRequisition(dep, pos);
        job.Id = jobId;
        context.Departments.Add(dep);
        context.Positions.Add(pos);
        
        var person = TestDatabase.Person("ANON-03");
        var candidate = TestDatabase.Candidate(person);
        candidate.Id = candidateId;
        candidate.Person = person;
        
        // Add one of each required navigation to verify it is loaded
        var competency = new Competency { Id = Guid.NewGuid(), Name = "C#", Code = "C#" };
        person.PersonCompetencies.Add(new PersonCompetency { Competency = competency, ExperienceMonths = 12 });
        person.EmploymentHistories.Add(new EmploymentHistory { EmployerName = "Company A", PositionTitle = "Dev", StartDate = DateOnly.FromDateTime(DateTime.UtcNow) });
        person.EducationRecords.Add(new EducationRecord { DegreeLevel = Domain.Enums.DegreeLevel.Bachelor, Institution = "MIT" });
        person.PersonCertificates.Add(new PersonCertificate { Certificate = new Certificate { Id = Guid.NewGuid(), Name = "AWS", Code = "AWS" } });
        person.PersonLanguages.Add(new PersonLanguage { Language = new Language { Id = Guid.NewGuid(), Name = "English", Code = "EN" }, IsNative = true });
        person.PersonSectorExperiences.Add(new PersonSectorExperience { Sector = new Sector { Id = Guid.NewGuid(), Name = "IT", Code = "IT" } });
        
        context.JobRequisitions.Add(job);
        context.Candidates.Add(candidate);
        await context.SaveChangesAsync();
        
        // Detach all so the service has to query them
        context.ChangeTracker.Clear();

        var batchResult = new CandidatePreScreeningBatchResult(jobId, 1, 1, 0, new[] { 
            CreateMockResult(candidateId, jobId, true)
        });
        
        _preScreeningMock.Setup(x => x.EvaluateApplicantsForJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<CandidatePreScreeningBatchResult>.Success(batchResult));

        _retrievalMock.Setup(x => x.RetrieveTopCandidatesAsync(It.IsAny<string>(), It.IsAny<IReadOnlyList<SemanticCandidateDocument>>(), 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<IReadOnlyList<SemanticRetrievalResult>>.Success(new List<SemanticRetrievalResult>()))
            .Callback<string, IReadOnlyList<SemanticCandidateDocument>, int, CancellationToken>((jDoc, cDocs, n, c) => 
            {
                var text = cDocs[0].Text;
                // Assert that the document text contains values from the navigations,
                // which proves the AsSplitQuery includes worked correctly.
                Assert.Contains("C#", text);
                Assert.Contains("Company A", text);
                Assert.Contains("MIT", text);
                Assert.Contains("AWS", text);
                Assert.Contains("English", text);
                Assert.Contains("IT", text);
            });

        var result = await sut.MatchApplicantsForJobAsync(jobId, 5);
        Assert.True(result.IsSuccess);
    }

    private static CandidatePreScreeningResult CreateMockResult(Guid candidateId, Guid jobId, bool isEligible)
    {
        return new CandidatePreScreeningResult(
            CandidateId: candidateId,
            JobRequisitionId: jobId,
            MandatorySkillResult: new SkillEvaluationResult(1, 1, 100.0m, EvaluationStatus.Pass, true, Array.Empty<Guid>(), Array.Empty<Guid>()),
            OverallSkillResult: new SkillEvaluationResult(1, 1, 100.0m, EvaluationStatus.Pass, true, Array.Empty<Guid>(), Array.Empty<Guid>()),
            ExperienceResult: new ExperienceEvaluationResult(12, 12, EvaluationStatus.Pass, true),
            EligibleForSemanticEvaluation: isEligible,
            FailureReasons: new List<PreScreeningFailureReason>(),
            MandatorySkillThresholdUsed: 100,
            OverallSkillThresholdUsed: 100
        );
    }
}
