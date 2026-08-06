using HrDecisionSupport.Application.EmployeeImports.Processing;

namespace HrDecisionSupport.Application.EmployeeImports.Persistence;

public interface IEmployeeImportOrchestrationHook
{
    void AfterCatalogPrepared();
    void BeforeRow(EmployeeImportNormalizedRow row);
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
