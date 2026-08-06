namespace HrDecisionSupport.Application.Common.Interfaces;

public interface IEmployeeImportTransactionRunner
{
    Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default);
}
