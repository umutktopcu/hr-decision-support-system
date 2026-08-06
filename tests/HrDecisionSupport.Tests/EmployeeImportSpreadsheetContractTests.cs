using HrDecisionSupport.Application.EmployeeImports;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;

namespace HrDecisionSupport.Tests;

public class EmployeeImportSpreadsheetContractTests
{
    [Fact]
    public void OrganizationCatalog_DefinesDeterministicDepartmentsAndBackendDeveloperMapping()
    {
        Assert.Equal(6, OrganizationCatalogDefinitions.Departments.Count);
        Assert.All(OrganizationCatalogDefinitions.Departments, department =>
            Assert.Equal(department.Code.ToUpperInvariant(), department.Code));
        Assert.Equal(
            "Information Technology",
            Assert.Single(OrganizationCatalogDefinitions.Departments,
                department => department.Code == "IT").DisplayName);
        Assert.Equal("BACKEND_DEVELOPER", OrganizationCatalogDefinitions.BackendDeveloper.Code);
        Assert.Equal("Backend Developer", OrganizationCatalogDefinitions.BackendDeveloper.DisplayName);
        Assert.Equal("IT", OrganizationCatalogDefinitions.BackendDeveloper.DefaultDepartmentCode);
    }

    [Fact]
    public void SpreadsheetHeaderContract_ContainsTheExactTwentyFourRequiredHeaders()
    {
        Assert.Equal(24, EmployeeImportSpreadsheetHeaders.Required.Count);
        Assert.Equal(
            "Anonim çalışan numarası",
            EmployeeImportSpreadsheetHeaders.Required[0]);
        Assert.Equal(
            "Kalış etiketi (0 Kısa, 1 Normal, 2 Uzun)",
            EmployeeImportSpreadsheetHeaders.Required[^1]);
        Assert.Equal(
            EmployeeImportSpreadsheetHeaders.Required.Count,
            EmployeeImportSpreadsheetHeaders.Required.Distinct(StringComparer.Ordinal).Count());
    }
}
