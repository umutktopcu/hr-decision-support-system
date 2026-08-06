using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;

namespace HrDecisionSupport.Infrastructure.EmployeeImports;

public sealed class EmployeeImportTransactionRunner(HrDecisionSupportDbContext context) : IEmployeeImportTransactionRunner
{
    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken = default)
    {
        var snapshot = CaptureTracker();
        await using IDbContextTransaction? transaction = await TryBeginAsync(cancellationToken);
        try { var result = await operation(cancellationToken); if (transaction is not null) await transaction.CommitAsync(cancellationToken); return result; }
        catch { if (transaction is not null) await transaction.RollbackAsync(CancellationToken.None); RestoreTracker(snapshot); throw; }
    }
    private async Task<IDbContextTransaction?> TryBeginAsync(CancellationToken ct)
    { try { return await context.Database.BeginTransactionAsync(ct); } catch (InvalidOperationException) { return null; } }

    private Dictionary<object, TrackedEntrySnapshot> CaptureTracker() => context.ChangeTracker.Entries()
        .ToDictionary(entry => entry.Entity, entry => new TrackedEntrySnapshot(entry.State, entry.CurrentValues.Clone(), entry.OriginalValues.Clone()), ReferenceEqualityComparer.Instance);

    private void RestoreTracker(IReadOnlyDictionary<object, TrackedEntrySnapshot> snapshot)
    {
        var newEntries = context.ChangeTracker.Entries().Where(entry => !snapshot.ContainsKey(entry.Entity)).ToList();
        while (newEntries.Count != 0)
        {
            var dependent = newEntries.FirstOrDefault(candidate => candidate.Metadata.GetForeignKeys().Any(foreignKey => newEntries.Any(principal => foreignKey.PrincipalEntityType == principal.Metadata)));
            var entry = dependent ?? newEntries[0];
            entry.State = EntityState.Detached;
            newEntries.Remove(entry);
        }
        foreach (var entry in context.ChangeTracker.Entries().ToArray())
        {
            if (!snapshot.TryGetValue(entry.Entity, out var prior)) continue;
            entry.CurrentValues.SetValues(prior.CurrentValues);
            entry.OriginalValues.SetValues(prior.OriginalValues);
            entry.State = prior.State;
        }
    }

    private sealed record TrackedEntrySnapshot(EntityState State, PropertyValues CurrentValues, PropertyValues OriginalValues);
}
