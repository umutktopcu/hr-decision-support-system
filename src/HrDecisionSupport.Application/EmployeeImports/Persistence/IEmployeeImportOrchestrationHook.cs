using HrDecisionSupport.Application.EmployeeImports.Processing;

namespace HrDecisionSupport.Application.EmployeeImports.Persistence;

public interface IEmployeeImportOrchestrationHook
{
    void AfterCatalogPrepared();
    void BeforeRow(EmployeeImportNormalizedRow row);
    void AfterRowPersistence(int trackedEntryCount) { }
    void BeforeTrackerCleanup(int trackedEntryCount) { }
    void AfterTrackerCleanup(int trackedEntryCount) { }
}

public sealed class NoOpEmployeeImportOrchestrationHook : IEmployeeImportOrchestrationHook
{
    public void AfterCatalogPrepared()
    {
    }

    public void BeforeRow(EmployeeImportNormalizedRow row)
    {
    }
}
