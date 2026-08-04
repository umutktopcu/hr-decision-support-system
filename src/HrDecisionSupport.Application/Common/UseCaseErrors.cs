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

    internal static readonly Error JobRequisitionNotFound =
        Error.NotFound("job_requisition_not_found", "The job requisition was not found.");

    internal static readonly Error JobRequisitionRequirementNotFound =
        Error.NotFound(
            "job_requisition_requirement_not_found",
            "The job requisition requirement was not found.");

    internal static readonly Error JobRequisitionStatusNoChange =
        Error.Conflict(
            "job_requisition_status_no_change",
            "The job requisition already has the requested status.");

    internal static readonly Error JobRequisitionStatusTransitionInvalid =
        Error.Conflict(
            "job_requisition_status_transition_invalid",
            "The requested job requisition status transition is not allowed.");

    internal static readonly Error JobRequisitionLocked =
        Error.Conflict(
            "job_requisition_locked",
            "The job requisition is locked against the requested change.");

    internal static readonly Error JobRequisitionConflict =
        Error.Conflict(
            "job_requisition_conflict",
            "The requisition code is already in use.");

    internal static readonly Error JobRequisitionRequirementConflict =
        Error.Conflict(
            "job_requisition_requirement_conflict",
            "The job requisition already has a requirement for this competency.");

    internal static readonly Error CandidateEvaluationCaseNotFound =
        Error.NotFound(
            "candidate_evaluation_case_not_found",
            "The candidate evaluation case was not found.");

    internal static readonly Error CandidateEvaluationCaseConflict =
        Error.Conflict(
            "candidate_evaluation_case_conflict",
            "The candidate already has an evaluation case for this job requisition.");

    internal static readonly Error CandidateEvaluationCaseLocked =
        Error.Conflict(
            "candidate_evaluation_case_locked",
            "The candidate evaluation case is locked against the requested change.");

    internal static readonly Error CandidateEvaluationCaseStatusNoChange =
        Error.Conflict(
            "candidate_evaluation_case_status_no_change",
            "The candidate evaluation case already has the requested status.");

    internal static readonly Error CandidateEvaluationCaseStatusTransitionInvalid =
        Error.Conflict(
            "candidate_evaluation_case_status_transition_invalid",
            "The requested candidate evaluation case status transition is not allowed.");

    internal static readonly Error CandidateEvaluationCaseRequisitionNotAvailable =
        Error.Conflict(
            "candidate_evaluation_case_requisition_not_available",
            "The job requisition is not available for a new candidate evaluation case.");

    internal static readonly Error EmployeeAssignmentNotFound =
        Error.NotFound("employee_assignment_not_found", "The employee assignment was not found.");

    internal static readonly Error CurrentEmployeeAssignmentNotFound =
        Error.NotFound(
            "current_employee_assignment_not_found",
            "The current employee assignment was not found.");

    internal static readonly Error EmployeeAssignmentOverlap =
        Error.Conflict(
            "employee_assignment_overlap",
            "The employee assignment overlaps another assignment.");

    internal static readonly Error EmployeeAssignmentOpenConflict =
        Error.Conflict(
            "employee_assignment_open_conflict",
            "The employee already has an open assignment.");

    internal static readonly Error EmployeeAssignmentStateConflict =
        Error.Conflict(
            "employee_assignment_state_conflict",
            "The employee has more than one open assignment.");

    internal static readonly Error EmployeeAssignmentAlreadyClosed =
        Error.Conflict(
            "employee_assignment_already_closed",
            "The employee assignment is already closed.");

    internal static readonly Error EmployeeAssignmentNoChange =
        Error.Conflict(
            "employee_assignment_no_change",
            "The new department and position match the current assignment.");

    internal static readonly Error EmployeeAssignmentBeforeHireDate =
        Error.Failure(
            "employee_assignment_before_hire_date",
            "The employee assignment cannot start before the employee hire date.");

    internal static readonly Error EmployeeAssignmentAfterTerminationDate =
        Error.Failure(
            "employee_assignment_after_termination_date",
            "The employee assignment cannot extend beyond the employee termination date.");

    internal static readonly Error EmployeeAssignmentEmployeeStateConflict =
        Error.Conflict(
            "employee_assignment_employee_state_conflict",
            "The employee status and termination date are inconsistent.");

    internal static readonly Error EmployeeAssignmentChangeDateInvalid =
        Error.Failure(
            "employee_assignment_change_date_invalid",
            "The new assignment must start after the current assignment.");

    internal static readonly Error EmployeeAssignmentEndBeforeStart =
        Error.Failure(
            "employee_assignment_end_before_start",
            "The employee assignment cannot end before it starts.");

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
