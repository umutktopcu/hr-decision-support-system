using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrDecisionSupport.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCoreSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "certificates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    issuer = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_certificates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "competencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    competency_category = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_competencies", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "departments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_departments", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "languages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_languages", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "people",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    anonymous_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    first_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: true),
                    phone_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_people", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "positions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_positions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_projects", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "sectors",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sectors", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "work_modes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_work_modes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "candidates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    candidate_source = table.Column<int>(type: "integer", nullable: false),
                    external_candidate_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidates", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidates_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "education_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    institution = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    field_of_study = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    degree_level = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    graduation_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_education_records", x => x.id);
                    table.CheckConstraint("ck_education_records_graduation_date_not_before_start_date", "start_date IS NULL OR graduation_date IS NULL OR graduation_date >= start_date");
                    table.ForeignKey(
                        name: "FK_education_records_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employees",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    hire_date = table.Column<DateOnly>(type: "date", nullable: false),
                    termination_date = table.Column<DateOnly>(type: "date", nullable: true),
                    employment_status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employees", x => x.id);
                    table.CheckConstraint("ck_employees_termination_date_not_before_hire_date", "termination_date IS NULL OR termination_date >= hire_date");
                    table.ForeignKey(
                        name: "FK_employees_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employment_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    employer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    position_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employment_histories", x => x.id);
                    table.CheckConstraint("ck_employment_histories_end_date_not_before_start_date", "end_date IS NULL OR end_date >= start_date");
                    table.ForeignKey(
                        name: "FK_employment_histories_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_certificates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    certificate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_date = table.Column<DateOnly>(type: "date", nullable: true),
                    expiration_date = table.Column<DateOnly>(type: "date", nullable: true),
                    credential_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_certificates", x => x.id);
                    table.CheckConstraint("ck_person_certificates_expiration_date_not_before_issue_date", "issue_date IS NULL OR expiration_date IS NULL OR expiration_date >= issue_date");
                    table.ForeignKey(
                        name: "FK_person_certificates_certificates_certificate_id",
                        column: x => x.certificate_id,
                        principalTable: "certificates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_certificates_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_competencies",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    experience_months = table.Column<int>(type: "integer", nullable: true),
                    proficiency_level = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_competencies", x => x.id);
                    table.CheckConstraint("ck_person_competencies_experience_months_non_negative", "experience_months IS NULL OR experience_months >= 0");
                    table.ForeignKey(
                        name: "FK_person_competencies_competencies_competency_id",
                        column: x => x.competency_id,
                        principalTable: "competencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_competencies_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_languages",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_id = table.Column<Guid>(type: "uuid", nullable: false),
                    proficiency_level = table.Column<int>(type: "integer", nullable: false),
                    is_native = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_languages", x => x.id);
                    table.ForeignKey(
                        name: "FK_person_languages_languages_language_id",
                        column: x => x.language_id,
                        principalTable: "languages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_languages_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_requisitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requisition_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    openings_count = table.Column<int>(type: "integer", nullable: false),
                    job_requisition_status = table.Column<int>(type: "integer", nullable: false),
                    opened_at = table.Column<DateOnly>(type: "date", nullable: false),
                    closed_at = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_requisitions", x => x.id);
                    table.CheckConstraint("ck_job_requisitions_closed_at_not_before_opened_at", "closed_at IS NULL OR closed_at >= opened_at");
                    table.CheckConstraint("ck_job_requisitions_openings_count", "openings_count > 0");
                    table.ForeignKey(
                        name: "FK_job_requisitions_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_requisitions_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_projects",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_projects", x => x.id);
                    table.CheckConstraint("ck_person_projects_end_date_not_before_start_date", "start_date IS NULL OR end_date IS NULL OR end_date >= start_date");
                    table.ForeignKey(
                        name: "FK_person_projects_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_projects_projects_project_id",
                        column: x => x.project_id,
                        principalTable: "projects",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_sector_experiences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sector_id = table.Column<Guid>(type: "uuid", nullable: false),
                    experience_months = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_sector_experiences", x => x.id);
                    table.CheckConstraint("ck_person_sector_experiences_experience_months_non_negative", "experience_months IS NULL OR experience_months >= 0");
                    table.ForeignKey(
                        name: "FK_person_sector_experiences_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_sector_experiences_sectors_sector_id",
                        column: x => x.sector_id,
                        principalTable: "sectors",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_work_mode_experiences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_mode_id = table.Column<Guid>(type: "uuid", nullable: false),
                    experience_months = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_work_mode_experiences", x => x.id);
                    table.CheckConstraint("ck_person_work_mode_experiences_experience_months_non_negative", "experience_months IS NULL OR experience_months >= 0");
                    table.ForeignKey(
                        name: "FK_person_work_mode_experiences_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_work_mode_experiences_work_modes_work_mode_id",
                        column: x => x.work_mode_id,
                        principalTable: "work_modes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_assignments",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    department_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position_id = table.Column<Guid>(type: "uuid", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: false),
                    end_date = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_assignments", x => x.id);
                    table.CheckConstraint("ck_employee_assignments_end_date_not_before_start_date", "end_date IS NULL OR end_date >= start_date");
                    table.ForeignKey(
                        name: "FK_employee_assignments_departments_department_id",
                        column: x => x.department_id,
                        principalTable: "departments",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_assignments_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_assignments_positions_position_id",
                        column: x => x.position_id,
                        principalTable: "positions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate_evaluation_cases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_requisition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    received_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_evaluation_cases", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_evaluation_cases_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_candidate_evaluation_cases_job_requisitions_job_requisition~",
                        column: x => x.job_requisition_id,
                        principalTable: "job_requisitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_requisition_requirements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_requisition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    competency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    minimum_experience_months = table.Column<int>(type: "integer", nullable: true),
                    minimum_proficiency_level = table.Column<int>(type: "integer", nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_requisition_requirements", x => x.id);
                    table.CheckConstraint("ck_job_requisition_requirements_min_exp_months_non_negative", "minimum_experience_months IS NULL OR minimum_experience_months >= 0");
                    table.ForeignKey(
                        name: "FK_job_requisition_requirements_competencies_competency_id",
                        column: x => x.competency_id,
                        principalTable: "competencies",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_job_requisition_requirements_job_requisitions_job_requisiti~",
                        column: x => x.job_requisition_id,
                        principalTable: "job_requisitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_candidate_evaluation_cases_candidate_id_job_requisition_id",
                table: "candidate_evaluation_cases",
                columns: new[] { "candidate_id", "job_requisition_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_evaluation_cases_job_requisition_id",
                table: "candidate_evaluation_cases",
                column: "job_requisition_id");

            migrationBuilder.CreateIndex(
                name: "IX_candidates_candidate_code",
                table: "candidates",
                column: "candidate_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidates_person_id",
                table: "candidates",
                column: "person_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_certificates_code",
                table: "certificates",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_competencies_code",
                table: "competencies",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_departments_code",
                table: "departments",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_education_records_person_id",
                table: "education_records",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_assignments_department_id",
                table: "employee_assignments",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_assignments_employee_id",
                table: "employee_assignments",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_assignments_position_id",
                table: "employee_assignments",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "IX_employees_employee_code",
                table: "employees",
                column: "employee_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employees_person_id",
                table: "employees",
                column: "person_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employment_histories_person_id",
                table: "employment_histories",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_requisition_requirements_competency_id",
                table: "job_requisition_requirements",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_requisition_requirements_job_requisition_id_competency_~",
                table: "job_requisition_requirements",
                columns: new[] { "job_requisition_id", "competency_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_requisitions_department_id",
                table: "job_requisitions",
                column: "department_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_requisitions_position_id",
                table: "job_requisitions",
                column: "position_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_requisitions_requisition_code",
                table: "job_requisitions",
                column: "requisition_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_languages_code",
                table: "languages",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_people_anonymous_code",
                table: "people",
                column: "anonymous_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_people_email",
                table: "people",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "IX_person_certificates_certificate_id",
                table: "person_certificates",
                column: "certificate_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_certificates_person_id_certificate_id",
                table: "person_certificates",
                columns: new[] { "person_id", "certificate_id" });

            migrationBuilder.CreateIndex(
                name: "IX_person_competencies_competency_id",
                table: "person_competencies",
                column: "competency_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_competencies_person_id_competency_id",
                table: "person_competencies",
                columns: new[] { "person_id", "competency_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_languages_language_id",
                table: "person_languages",
                column: "language_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_languages_person_id_language_id",
                table: "person_languages",
                columns: new[] { "person_id", "language_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_projects_person_id",
                table: "person_projects",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_projects_project_id",
                table: "person_projects",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_sector_experiences_person_id_sector_id",
                table: "person_sector_experiences",
                columns: new[] { "person_id", "sector_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_sector_experiences_sector_id",
                table: "person_sector_experiences",
                column: "sector_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_work_mode_experiences_person_id_work_mode_id",
                table: "person_work_mode_experiences",
                columns: new[] { "person_id", "work_mode_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_work_mode_experiences_work_mode_id",
                table: "person_work_mode_experiences",
                column: "work_mode_id");

            migrationBuilder.CreateIndex(
                name: "IX_positions_code",
                table: "positions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sectors_code",
                table: "sectors",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_work_modes_code",
                table: "work_modes",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_evaluation_cases");

            migrationBuilder.DropTable(
                name: "education_records");

            migrationBuilder.DropTable(
                name: "employee_assignments");

            migrationBuilder.DropTable(
                name: "employment_histories");

            migrationBuilder.DropTable(
                name: "job_requisition_requirements");

            migrationBuilder.DropTable(
                name: "person_certificates");

            migrationBuilder.DropTable(
                name: "person_competencies");

            migrationBuilder.DropTable(
                name: "person_languages");

            migrationBuilder.DropTable(
                name: "person_projects");

            migrationBuilder.DropTable(
                name: "person_sector_experiences");

            migrationBuilder.DropTable(
                name: "person_work_mode_experiences");

            migrationBuilder.DropTable(
                name: "candidates");

            migrationBuilder.DropTable(
                name: "employees");

            migrationBuilder.DropTable(
                name: "job_requisitions");

            migrationBuilder.DropTable(
                name: "certificates");

            migrationBuilder.DropTable(
                name: "competencies");

            migrationBuilder.DropTable(
                name: "languages");

            migrationBuilder.DropTable(
                name: "projects");

            migrationBuilder.DropTable(
                name: "sectors");

            migrationBuilder.DropTable(
                name: "work_modes");

            migrationBuilder.DropTable(
                name: "people");

            migrationBuilder.DropTable(
                name: "departments");

            migrationBuilder.DropTable(
                name: "positions");
        }
    }
}
