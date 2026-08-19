using HrDecisionSupport.Application.Common;

namespace HrDecisionSupport.Application.PositionBenchmarks;

public interface IPositionBenchmarkService
{
    Task<Result<PositionBenchmarkResult>> GetBenchmarkAsync(
        Guid positionId,
        CancellationToken cancellationToken = default);
}
