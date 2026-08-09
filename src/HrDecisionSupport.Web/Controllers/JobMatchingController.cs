using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.MatchingExecution;
using HrDecisionSupport.Application.MatchingExecution.Models;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HrDecisionSupport.Web.Controllers;

[ApiController]
[Route("api/job-requisitions/{jobId}/matching")]
public class JobMatchingController : ControllerBase
{
    private readonly IJobMatchingExecutionService _service;

    public JobMatchingController(IJobMatchingExecutionService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> ExecuteMatching(
        [FromRoute] Guid jobId,
        [FromBody] JobMatchingRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.ExecuteMatchingAsync(jobId, request, cancellationToken);
        return MapResult(result);
    }

    private IActionResult MapResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        var error = result.Error;
        if (error == null) return StatusCode(500);

        if (error.Type == ErrorType.Validation)
        {
            return BadRequest(new { errors = result.Errors });
        }
        else if (error.Type == ErrorType.NotFound)
        {
            return NotFound(new { error.Code, error.Message });
        }
        else if (error.Type == ErrorType.Conflict)
        {
            return Conflict(new { error.Code, error.Message });
        }
        else
        {
            return BadRequest(new { error.Code, error.Message });
        }
    }
}
