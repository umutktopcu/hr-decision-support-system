using HrDecisionSupport.Application.EmployeeImports.Processing;

namespace HrDecisionSupport.Application.EmployeeImports.Persistence;

public interface IEmployeeImportRowPersistenceHook
{
    void AfterPersonAndEmployeePrepared(EmployeeImportNormalizedRow row);
}

public sealed class NoOpEmployeeImportRowPersistenceHook : IEmployeeImportRowPersistenceHook
{
    public void AfterPersonAndEmployeePrepared(EmployeeImportNormalizedRow row)
    {
    }
}
