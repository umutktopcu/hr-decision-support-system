using HrDecisionSupport.Application.MatchingExecution.Models;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using System;
using System.Threading.Tasks;
using System.Threading;
using Moq;
using Xunit;
using Microsoft.AspNetCore.Mvc;
using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Tests.Web;

[Collection(PostgreSqlIntegrationCollection.Name)]
public class JobMatchingControllerIntegrationTests
{
    private readonly PostgreSqlIntegrationTestFixture _fixture;

    public JobMatchingControllerIntegrationTests(PostgreSqlIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgreSqlIntegrationFact]
    public async Task ExecuteMatching_ReturnsExpectedResults()
    {
        await _fixture.ResetDatabaseAsync();
        await using var context = _fixture.CreateDbContext();

        var department = TestDatabase.Department();
        var position = TestDatabase.Position();

        var req = new JobRequisition
        {
            Id = Guid.NewGuid(),
            RequisitionCode = "TEST-MATCH-001",
            Title = "Backend Dev",
            DepartmentId = department.Id,
            PositionId = position.Id,
            CreatedAtUtc = DateTime.UtcNow,
            OpeningsCount = 1,
            JobRequisitionStatus = JobRequisitionStatus.Draft
        };

        var cand1 = new Candidate { Id = Guid.NewGuid(), CandidateCode = "C1" };

        context.AddRange(department, position, req, cand1);
        await context.SaveChangesAsync();

        var serviceMock = new Mock<HrDecisionSupport.Application.MatchingExecution.IJobMatchingExecutionService>();
        var matchingResult = Result<JobMatchingExecutionResult>.Success(new JobMatchingExecutionResult(
            req.Id,
            new JobMatchingStatistics(1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1),
            null,
            new System.Collections.Generic.List<JobMatchingCandidateResult>()
        ));
        serviceMock.Setup(x => x.ExecuteMatchingAsync(req.Id, It.IsAny<JobMatchingRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(matchingResult);

        var controller = new HrDecisionSupport.Web.Controllers.JobMatchingController(serviceMock.Object);

        var request = new JobMatchingRequest(10, 5);
        var response = await controller.ExecuteMatching(req.Id, request, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(response);
        var result = Assert.IsType<JobMatchingExecutionResult>(okResult.Value);
        Assert.NotNull(result);
        Assert.NotNull(result.Statistics);
    }
}
