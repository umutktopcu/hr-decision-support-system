using HrDecisionSupport.Infrastructure.EmployeeImports;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

public sealed class EmployeeImportTransactionRunnerTests
{
    [Fact]
    public async Task ExecuteAsync_WhenOperationFails_RestoresTrackedEntriesAndDetachesNewEntries()
    {
        await using var context = TestDatabase.CreateContext(); var existing = TestDatabase.Person("EXISTING"); context.People.Add(existing); await context.SaveChangesAsync(); var runner = new EmployeeImportTransactionRunner(context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ExecuteAsync<int>(_ =>
        {
            existing.FirstName = "Changed"; context.People.Add(TestDatabase.Person("NEW")); return Task.FromException<int>(new InvalidOperationException());
        }));
        Assert.Equal("Ada", existing.FirstName); Assert.Equal(EntityState.Unchanged, context.Entry(existing).State); Assert.DoesNotContain(context.ChangeTracker.Entries(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNewEmployeeRowFails_AllowsFailureRowSaveWithoutRequiredRelationshipError()
    {
        await using var context = TestDatabase.CreateContext(); var batch = new EmployeeImportBatch { Id = Guid.NewGuid(), FileName = "source.xlsx", FileHash = new string('a', 64), DatasetSplit = EmployeeDatasetSplit.Training, ImportedAtUtc = DateTime.UtcNow, Status = EmployeeImportBatchStatus.Processing }; context.EmployeeImportBatches.Add(batch); await context.SaveChangesAsync(); var runner = new EmployeeImportTransactionRunner(context);
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ExecuteAsync<int>(_ =>
        {
            var person = TestDatabase.Person("FAILED"); context.People.Add(person); context.Employees.Add(new Employee { Id = Guid.NewGuid(), PersonId = person.Id, EmployeeCode = "FAILED", HireDate = new DateOnly(2020, 1, 1), EmploymentStatus = EmploymentStatus.Active }); return Task.FromException<int>(new InvalidOperationException());
        }));
        context.EmployeeImportRows.Add(new EmployeeImportRow { Id = Guid.NewGuid(), ImportBatchId = batch.Id, SourceRowNumber = 2, ExternalEmployeeCode = "FAILED", RawPayloadJson = "{}", ValidationErrorsJson = "[]", ImportStatus = EmployeeImportRowStatus.Failed, CreatedAtUtc = DateTime.UtcNow }); await context.SaveChangesAsync();
        Assert.Empty(context.People); Assert.Empty(context.Employees); Assert.Single(context.EmployeeImportRows);
    }
}
