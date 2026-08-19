using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.PositionBenchmarks;
using Microsoft.AspNetCore.Mvc;

namespace HrDecisionSupport.Web.Controllers;

[ApiController]
[Route("api/position-benchmarks")]
public sealed class PositionBenchmarksController : ControllerBase
{
    private readonly IPositionBenchmarkService _service;
    private readonly ILogger<PositionBenchmarksController> _logger;

    public PositionBenchmarksController(
        IPositionBenchmarkService service,
        ILogger<PositionBenchmarksController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("{positionId:guid}")]
    public async Task<IActionResult> Get(
        [FromRoute] Guid positionId,
        CancellationToken cancellationToken)
    {
        Result<PositionBenchmarkResult> result;
        try
        {
            result = await _service.GetBenchmarkAsync(positionId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Position benchmark request failed for position {PositionId}.",
                positionId);
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Position benchmark could not be retrieved.");
        }
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var error = result.Error;
        if (error is null)
        {
            return Problem(
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Position benchmark could not be retrieved.");
        }

        var response = new { error.Code, error.Message };
        return error.Type switch
        {
            ErrorType.NotFound => NotFound(response),
            ErrorType.Conflict => Conflict(response),
            _ => BadRequest(response)
        };
    }
}
