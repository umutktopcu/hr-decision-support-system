using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrDecisionSupport.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmployeeExcelImportFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_employee_assignments_end_date_not_before_start_date",
                table: "employee_assignments");

            migrationBuilder.AlterColumn<int>(
                name: "proficiency_level",
                table: "person_languages",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<DateOnly>(
                name: "start_date",
                table: "employee_assignments",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateOnly),
                oldType: "date");

            migrationBuilder.AlterColumn<string>(
                name: "institution",
                table: "education_records",
                type: "character varying(250)",
                maxLength: 250,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250);

            migrationBuilder.CreateTable(
                name: "employee_import_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    file_name = table.Column<string>(type: "character varying(260)", maxLength: 260, nullable: false),
                    file_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dataset_split = table.Column<int>(type: "integer", nullable: false),
                    observation_date = table.Column<DateOnly>(type: "date", nullable: true),
                    imported_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    total_row_count = table.Column<int>(type: "integer", nullable: false),
                    successful_row_count = table.Column<int>(type: "integer", nullable: false),
                    failed_row_count = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_import_batches", x => x.id);
                    table.CheckConstraint("ck_emp_import_batch_completed_counts_lte_total", "successful_row_count + failed_row_count <= total_row_count");
                    table.CheckConstraint("ck_employee_import_batches_file_hash_lowercase_sha256", "file_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_employee_import_batches_row_counts_non_negative", "total_row_count >= 0 AND successful_row_count >= 0 AND failed_row_count >= 0");
                });

            migrationBuilder.CreateTable(
                name: "employee_career_feature_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observed_at = table.Column<DateOnly>(type: "date", nullable: true),
                    total_experience_months = table.Column<int>(type: "integer", nullable: true),
                    backend_experience_months = table.Column<int>(type: "integer", nullable: true),
                    has_previous_company = table.Column<bool>(type: "boolean", nullable: true),
                    previous_company_average_stay_months = table.Column<int>(type: "integer", nullable: true),
                    shortest_previous_job_months = table.Column<int>(type: "integer", nullable: true),
                    longest_previous_job_months = table.Column<int>(type: "integer", nullable: true),
                    last_previous_company_stay_months = table.Column<int>(type: "integer", nullable: true),
                    company_change_count = table.Column<int>(type: "integer", nullable: true),
                    imported_job_change_rate = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    observed_company_tenure_months = table.Column<int>(type: "integer", nullable: true),
                    feature_source = table.Column<int>(type: "integer", nullable: false),
                    feature_schema_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_career_feature_snapshots", x => x.id);
                    table.CheckConstraint("ck_emp_feature_backend_lte_total", "backend_experience_months IS NULL OR total_experience_months IS NULL OR backend_experience_months <= total_experience_months");
                    table.CheckConstraint("ck_emp_feature_job_change_rate_non_negative", "imported_job_change_rate IS NULL OR imported_job_change_rate >= 0");
                    table.CheckConstraint("ck_emp_feature_shortest_lte_longest", "shortest_previous_job_months IS NULL OR longest_previous_job_months IS NULL OR shortest_previous_job_months <= longest_previous_job_months");
                    table.CheckConstraint("ck_employee_career_feature_snapshots_months_non_negative", "(total_experience_months IS NULL OR total_experience_months >= 0) AND (backend_experience_months IS NULL OR backend_experience_months >= 0) AND (previous_company_average_stay_months IS NULL OR previous_company_average_stay_months >= 0) AND (shortest_previous_job_months IS NULL OR shortest_previous_job_months >= 0) AND (longest_previous_job_months IS NULL OR longest_previous_job_months >= 0) AND (last_previous_company_stay_months IS NULL OR last_previous_company_stay_months >= 0) AND (company_change_count IS NULL OR company_change_count >= 0) AND (observed_company_tenure_months IS NULL OR observed_company_tenure_months >= 0)");
                    table.ForeignKey(
                        name: "FK_employee_career_feature_snapshots_employee_import_batches_i~",
                        column: x => x.import_batch_id,
                        principalTable: "employee_import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_career_feature_snapshots_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_import_rows",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_row_number = table.Column<int>(type: "integer", nullable: false),
                    external_employee_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    raw_payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    import_status = table.Column<int>(type: "integer", nullable: false),
                    validation_errors_json = table.Column<string>(type: "jsonb", nullable: true),
                    employee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_import_rows", x => x.id);
                    table.CheckConstraint("ck_employee_import_rows_source_row_number_positive", "source_row_number > 0");
                    table.ForeignKey(
                        name: "FK_employee_import_rows_employee_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "employee_import_batches",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_employee_import_rows_employees_employee_id",
                        column: x => x.employee_id,
                        principalTable: "employees",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "employee_retention_labels",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    employee_career_feature_snapshot_id = table.Column<Guid>(type: "uuid", nullable: false),
                    label = table.Column<int>(type: "integer", nullable: false),
                    label_source = table.Column<int>(type: "integer", nullable: false),
                    label_definition_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_employee_retention_labels", x => x.id);
                    table.CheckConstraint("ck_employee_retention_labels_label_valid", "label IN (0, 1, 2)");
                    table.ForeignKey(
                        name: "FK_employee_retention_labels_employee_career_feature_snapshots~",
                        column: x => x.employee_career_feature_snapshot_id,
                        principalTable: "employee_career_feature_snapshots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_prior_position_evidences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    sequence_number = table.Column<int>(type: "integer", nullable: false),
                    import_row_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_person_prior_position_evidences", x => x.id);
                    table.CheckConstraint("ck_person_prior_position_evidences_sequence_number_positive", "sequence_number > 0");
                    table.ForeignKey(
                        name: "FK_person_prior_position_evidences_employee_import_rows_import~",
                        column: x => x.import_row_id,
                        principalTable: "employee_import_rows",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_person_prior_position_evidences_people_person_id",
                        column: x => x.person_id,
                        principalTable: "people",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_employee_assignments_end_date_not_before_start_date",
                table: "employee_assignments",
                sql: "start_date IS NULL OR end_date IS NULL OR end_date >= start_date");

            migrationBuilder.CreateIndex(
                name: "IX_employee_career_feature_snapshots_employee_id_import_batch_~",
                table: "employee_career_feature_snapshots",
                columns: new[] { "employee_id", "import_batch_id", "feature_schema_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_career_feature_snapshots_import_batch_id",
                table: "employee_career_feature_snapshots",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_import_batches_file_hash",
                table: "employee_import_batches",
                column: "file_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_import_rows_employee_id",
                table: "employee_import_rows",
                column: "employee_id");

            migrationBuilder.CreateIndex(
                name: "IX_employee_import_rows_import_batch_id_external_employee_code",
                table: "employee_import_rows",
                columns: new[] { "import_batch_id", "external_employee_code" },
                unique: true,
                filter: "\"external_employee_code\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_employee_import_rows_import_batch_id_source_row_number",
                table: "employee_import_rows",
                columns: new[] { "import_batch_id", "source_row_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_employee_retention_labels_employee_career_feature_snapshot_~",
                table: "employee_retention_labels",
                column: "employee_career_feature_snapshot_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_person_prior_position_evidences_import_row_id",
                table: "person_prior_position_evidences",
                column: "import_row_id");

            migrationBuilder.CreateIndex(
                name: "IX_person_prior_position_evidences_person_id_sequence_number_t~",
                table: "person_prior_position_evidences",
                columns: new[] { "person_id", "sequence_number", "title" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "employee_retention_labels");

            migrationBuilder.DropTable(
                name: "person_prior_position_evidences");

            migrationBuilder.DropTable(
                name: "employee_career_feature_snapshots");

            migrationBuilder.DropTable(
                name: "employee_import_rows");

            migrationBuilder.DropTable(
                name: "employee_import_batches");

            migrationBuilder.DropCheckConstraint(
                name: "ck_employee_assignments_end_date_not_before_start_date",
                table: "employee_assignments");

            migrationBuilder.AlterColumn<int>(
                name: "proficiency_level",
                table: "person_languages",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AlterColumn<DateOnly>(
                name: "start_date",
                table: "employee_assignments",
                type: "date",
                nullable: false,
                defaultValue: new DateOnly(1, 1, 1),
                oldClrType: typeof(DateOnly),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "institution",
                table: "education_records",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(250)",
                oldMaxLength: 250,
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_employee_assignments_end_date_not_before_start_date",
                table: "employee_assignments",
                sql: "end_date IS NULL OR end_date >= start_date");
        }
    }
}
