using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.PositionBenchmarks;
using HrDecisionSupport.Domain.Enums;
using HrDecisionSupport.Web.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace HrDecisionSupport.Tests.Web;

public sealed class PositionBenchmarksControllerTests
{
    [Fact]
    public async Task Get_ValidPosition_ReturnsTypedServiceResult()
    {
        var positionId = Guid.NewGuid();
        var benchmark = CreateBenchmark(positionId);
        var service = new Mock<IPositionBenchmarkService>(MockBehavior.Strict);
        service.Setup(x => x.GetBenchmarkAsync(positionId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PositionBenchmarkResult>.Success(benchmark));
        var controller = CreateController(service.Object);

        var response = await controller.Get(positionId, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(response);
        Assert.Same(benchmark, ok.Value);
        service.VerifyAll();
    }

    [Fact]
    public async Task Get_UnknownPosition_ReturnsControlledNotFound()
    {
        var result = await ExecuteFailureAsync(
            Error.NotFound("position_not_found", "The position was not found."));

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        AssertErrorResponse(notFound.Value, "position_not_found");
    }

    [Fact]
    public async Task Get_InactivePosition_ReturnsControlledBadRequest()
    {
        var result = await ExecuteFailureAsync(
            Error.Failure("position_inactive", "The position is inactive."));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        AssertErrorResponse(badRequest.Value, "position_inactive");
    }

    [Fact]
    public async Task Get_ServiceFailure_ReturnsControlledBadRequest()
    {
        var result = await ExecuteFailureAsync(
            Error.Failure("benchmark_failed", "The benchmark could not be calculated."));

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        AssertErrorResponse(badRequest.Value, "benchmark_failed");
    }

    [Fact]
    public async Task Get_Cancellation_PropagatesToServiceAndCaller()
    {
        var positionId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var service = new Mock<IPositionBenchmarkService>(MockBehavior.Strict);
        service.Setup(x => x.GetBenchmarkAsync(positionId, cancellation.Token))
            .ThrowsAsync(new OperationCanceledException(cancellation.Token));
        var controller = CreateController(service.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => controller.Get(positionId, cancellation.Token));

        service.VerifyAll();
    }

    [Fact]
    public async Task Get_UnexpectedServiceException_ReturnsGenericProblemWithoutDetails()
    {
        var service = new Mock<IPositionBenchmarkService>(MockBehavior.Strict);
        service.Setup(x => x.GetBenchmarkAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("sensitive database detail"));
        var controller = CreateController(service.Object);

        var response = await controller.Get(Guid.NewGuid(), CancellationToken.None);

        var problem = Assert.IsType<ObjectResult>(response);
        Assert.Equal(500, problem.StatusCode);
        var details = Assert.IsType<ProblemDetails>(problem.Value);
        Assert.Equal("Position benchmark could not be retrieved.", details.Title);
        Assert.DoesNotContain("sensitive database detail", details.ToString());
    }

    [Fact]
    public void Controller_DependsOnlyOnPositionBenchmarkService()
    {
        var constructor = Assert.Single(typeof(PositionBenchmarksController).GetConstructors());
        var parameters = constructor.GetParameters();

        Assert.Equal(2, parameters.Length);
        Assert.Equal(typeof(IPositionBenchmarkService), parameters[0].ParameterType);
        Assert.Equal(typeof(ILogger<PositionBenchmarksController>), parameters[1].ParameterType);
    }

    private static async Task<IActionResult> ExecuteFailureAsync(Error error)
    {
        var service = new Mock<IPositionBenchmarkService>(MockBehavior.Strict);
        service.Setup(x => x.GetBenchmarkAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result<PositionBenchmarkResult>.Failure(error));
        var controller = CreateController(service.Object);
        return await controller.Get(Guid.NewGuid(), CancellationToken.None);
    }

    private static PositionBenchmarksController CreateController(IPositionBenchmarkService service) =>
        new(service, NullLogger<PositionBenchmarksController>.Instance);

    private static void AssertErrorResponse(object? value, string expectedCode)
    {
        var json = System.Text.Json.JsonSerializer.Serialize(value);
        using var document = System.Text.Json.JsonDocument.Parse(json);
        Assert.Equal(expectedCode, document.RootElement.GetProperty("Code").GetString());
        Assert.False(string.IsNullOrWhiteSpace(document.RootElement.GetProperty("Message").GetString()));
    }

    private static PositionBenchmarkResult CreateBenchmark(Guid positionId)
    {
        var coverage = new BenchmarkProfileCoverage(3, 3);
        return new PositionBenchmarkResult(
            positionId,
            "BACKEND_DEVELOPER",
            "Backend Developer",
            3,
            PositionBenchmarkSampleSizeStatus.Sufficient,
            new PositionWorkforceBenchmark(
                new PositionSkillBenchmark(coverage, []),
                new PositionExperienceBenchmark(true, coverage, 36m),
                new PositionEducationBenchmark(coverage, []),
                new PositionLanguageBenchmark(coverage, [])),
            new PositionRequirementSuggestions([], 36, DegreeLevel.Bachelor, []));
    }
}
