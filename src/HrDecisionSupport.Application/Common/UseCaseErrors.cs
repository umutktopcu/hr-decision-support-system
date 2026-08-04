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

    internal static readonly Error PersonNotFound =
        Error.NotFound("person_not_found", "The person was not found.");

    internal static readonly Error CompetencyNotFound =
        Error.NotFound("competency_not_found", "The competency was not found.");

    internal static readonly Error CompetencyInactive =
        Error.Failure("competency_inactive", "The competency is inactive.");

    internal static readonly Error PersonCompetencyNotFound =
        Error.NotFound("person_competency_not_found", "The person competency was not found.");

    internal static readonly Error PersonCompetencyConflict =
        Error.Conflict(
            "person_competency_conflict",
            "The person already has this competency.");

    internal static readonly Error EducationRecordNotFound =
        Error.NotFound("education_record_not_found", "The education record was not found.");

    internal static readonly Error CertificateNotFound =
        Error.NotFound("certificate_not_found", "The certificate was not found.");

    internal static readonly Error PersonCertificateNotFound =
        Error.NotFound("person_certificate_not_found", "The person certificate was not found.");

    internal static readonly Error LanguageNotFound =
        Error.NotFound("language_not_found", "The language was not found.");

    internal static readonly Error PersonLanguageNotFound =
        Error.NotFound("person_language_not_found", "The person language was not found.");

    internal static readonly Error PersonLanguageConflict =
        Error.Conflict("person_language_conflict", "The person already has this language.");

    internal static readonly Error EmploymentHistoryNotFound =
        Error.NotFound("employment_history_not_found", "The employment history was not found.");

    internal static readonly Error ProjectNotFound =
        Error.NotFound("project_not_found", "The project was not found.");

    internal static readonly Error PersonProjectNotFound =
        Error.NotFound("person_project_not_found", "The person project was not found.");

    internal static readonly Error SectorNotFound =
        Error.NotFound("sector_not_found", "The sector was not found.");

    internal static readonly Error PersonSectorExperienceNotFound =
        Error.NotFound(
            "person_sector_experience_not_found",
            "The person sector experience was not found.");

    internal static readonly Error PersonSectorExperienceConflict =
        Error.Conflict(
            "person_sector_experience_conflict",
            "The person already has experience for this sector.");

    internal static readonly Error WorkModeNotFound =
        Error.NotFound("work_mode_not_found", "The work mode was not found.");

    internal static readonly Error PersonWorkModeExperienceNotFound =
        Error.NotFound(
            "person_work_mode_experience_not_found",
            "The person work mode experience was not found.");

    internal static readonly Error PersonWorkModeExperienceConflict =
        Error.Conflict(
            "person_work_mode_experience_conflict",
            "The person already has experience for this work mode.");
}
