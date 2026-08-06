using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Storage;

namespace HrDecisionSupport.Infrastructure.EmployeeImports;

public sealed class EmployeeImportTransactionRunner(HrDecisionSupportDbContext context) : IEmployeeImportTransactionRunner
{
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        await using IDbContextTransaction? transaction = await TryBeginAsync(cancellationToken);
        try { var result = await operation(cancellationToken); if (transaction is not null) await transaction.CommitAsync(cancellationToken); return result; }
        catch { if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None); throw; }
    }
    private async Task<IDbContextTransaction?> TryBeginAsync(CancellationToken ct)
    { try { return await context.Database.BeginTransactionAsync(ct); } catch (InvalidOperationException) { return null; } }
}
