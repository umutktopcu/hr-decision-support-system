using System;
using System.Threading;
using System.Threading.Tasks;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.MatchingExecution.Models;

namespace HrDecisionSupport.Application.MatchingExecution;

public interface IJobMatchingExecutionService
{
    Task<Result<JobMatchingExecutionResult>> ExecuteMatchingAsync(
        Guid jobRequisitionId,
        JobMatchingRequest request,
        CancellationToken cancellationToken = default);
}
