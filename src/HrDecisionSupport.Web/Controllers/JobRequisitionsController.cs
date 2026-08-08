using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Requisitions;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace HrDecisionSupport.Web.Controllers;

[ApiController]
[Route("api/job-requisitions")]
public class JobRequisitionsController : ControllerBase
{
    private readonly IJobRequisitionService _service;

    public JobRequisitionsController(IJobRequisitionService service)
    {
        _service = service;
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [FromBody] CreateJobRequisitionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.CreateAsync(request, cancellationToken);
        return MapResult(result, createdId => CreatedAtAction(nameof(Get), new { id = createdId }, result.Value));
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _service.GetByIdAsync(id, cancellationToken);
        return MapResult(result);
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await _service.ListAsync(cancellationToken);
        return MapResult(result);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateJobRequisitionRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.UpdateAsync(id, request, cancellationToken);
        return MapResult(result);
    }

    [HttpPatch("{id}/status")]
    public async Task<IActionResult> ChangeStatus(
        [FromRoute] Guid id,
        [FromBody] ChangeJobRequisitionStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _service.ChangeStatusAsync(id, request, cancellationToken);
        return MapResult(result);
    }

    private IActionResult MapResult<T>(Result<T> result, Func<Guid, IActionResult>? createdResult = null)
    {
        if (result.IsSuccess)
        {
            if (createdResult != null && result.Value is JobRequisitionDto dto)
            {
                return createdResult(dto.Id);
            }
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
