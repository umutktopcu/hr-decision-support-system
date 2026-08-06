namespace HrDecisionSupport.Application.EmployeeImports;

public sealed record OrganizationDepartmentDefinition(string Code, string DisplayName);

public sealed record OrganizationPositionDefinition(
    string Code,
    string DisplayName,
    string DefaultDepartmentCode);

public static class OrganizationCatalogDefinitions
{
    public const string InformationTechnologyDepartmentCode = "IT";
    public const string BackendDeveloperPositionCode = "BACKEND_DEVELOPER";

    public static IReadOnlyList<OrganizationDepartmentDefinition> Departments { get; } =
    [
        new("IT", "Information Technology"),
        new("HR", "Human Resources"),
        new("FINANCE", "Finance"),
        new("SALES", "Sales"),
        new("MARKETING", "Marketing"),
        new("OPERATIONS", "Operations")
    ];

    public static OrganizationPositionDefinition BackendDeveloper { get; } =
        new(BackendDeveloperPositionCode, "Backend Developer", InformationTechnologyDepartmentCode);
}
