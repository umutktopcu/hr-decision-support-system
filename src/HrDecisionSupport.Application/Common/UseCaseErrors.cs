namespace HrDecisionSupport.Application.Common;

internal static class UseCaseErrors
{
    internal static Error EmployeeNotFound(Guid id) =>
        Error.NotFound("employee_not_found", $"Employee '{id}' was not found.");

    internal static readonly Error EmployeeCodeConflict =
        Error.Conflict("employee_code_conflict", "The employee code is already in use.");

    internal static readonly Error CandidateNotFound =
        Error.NotFound("candidate_not_found", "The candidate was not found.");

    internal static readonly Error CandidateCodeConflict =
        Error.Conflict("candidate_code_conflict", "The candidate code is already in use.");

    internal static readonly Error PersonDataConflict =
        Error.Conflict(
            "person_data_conflict",
            "The supplied person data conflicts with the existing person record.");

    internal static readonly Error PersonAlreadyEmployee =
        Error.Conflict(
            "person_employee_role_conflict",
            "The person already has an employee record.");

    internal static readonly Error PersonAlreadyCandidate =
        Error.Conflict(
            "person_candidate_role_conflict",
            "The person already has a candidate record.");

    internal static readonly Error DepartmentNotFound =
        Error.NotFound("department_not_found", "The department was not found.");

    internal static readonly Error DepartmentInactive =
        Error.Failure("department_inactive", "The department is inactive.");

    internal static readonly Error PositionNotFound =
        Error.NotFound("position_not_found", "The position was not found.");

    internal static readonly Error PositionInactive =
        Error.Failure("position_inactive", "The position is inactive.");
}
