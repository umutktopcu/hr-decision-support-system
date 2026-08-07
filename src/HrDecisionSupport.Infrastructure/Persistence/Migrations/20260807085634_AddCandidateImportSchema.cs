using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrDecisionSupport.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCandidateImportSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "availability_days",
                table: "candidates",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "professional_title",
                table: "candidates",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "candidate_career_feature_snapshots",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    total_experience_months = table.Column<int>(type: "integer", nullable: true),
                    backend_experience_months = table.Column<int>(type: "integer", nullable: true),
                    previous_company_average_stay_months = table.Column<decimal>(type: "numeric(6,1)", precision: 6, scale: 1, nullable: true),
                    shortest_previous_job_months = table.Column<int>(type: "integer", nullable: true),
                    longest_previous_job_months = table.Column<int>(type: "integer", nullable: true),
                    last_previous_company_stay_months = table.Column<int>(type: "integer", nullable: true),
                    company_change_count = table.Column<int>(type: "integer", nullable: true),
                    job_change_rate = table.Column<decimal>(type: "numeric(12,6)", precision: 12, scale: 6, nullable: true),
                    feature_schema_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    calculated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_career_feature_snapshots", x => x.id);
                    table.CheckConstraint("ck_candidate_career_feature_snapshots_non_negative", "(total_experience_months IS NULL OR total_experience_months >= 0) AND (backend_experience_months IS NULL OR backend_experience_months >= 0) AND (previous_company_average_stay_months IS NULL OR previous_company_average_stay_months >= 0) AND (shortest_previous_job_months IS NULL OR shortest_previous_job_months >= 0) AND (longest_previous_job_months IS NULL OR longest_previous_job_months >= 0) AND (last_previous_company_stay_months IS NULL OR last_previous_company_stay_months >= 0) AND (company_change_count IS NULL OR company_change_count >= 0) AND (job_change_rate IS NULL OR job_change_rate >= 0)");
                    table.CheckConstraint("ck_candidate_feature_backend_lte_total", "backend_experience_months IS NULL OR total_experience_months IS NULL OR backend_experience_months <= total_experience_months");
                    table.CheckConstraint("ck_candidate_feature_shortest_lte_longest", "shortest_previous_job_months IS NULL OR longest_previous_job_months IS NULL OR shortest_previous_job_months <= longest_previous_job_months");
                    table.ForeignKey(
                        name: "FK_candidate_career_feature_snapshots_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "candidate_work_mode_preferences",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_mode_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_candidate_work_mode_preferences", x => x.id);
                    table.ForeignKey(
                        name: "FK_candidate_work_mode_preferences_candidates_candidate_id",
                        column: x => x.candidate_id,
                        principalTable: "candidates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_candidate_work_mode_preferences_work_modes_work_mode_id",
                        column: x => x.work_mode_id,
                        principalTable: "work_modes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_candidates_availability_days_non_negative",
                table: "candidates",
                sql: "availability_days IS NULL OR availability_days >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_candidate_career_feature_snapshots_candidate_id_feature_sch~",
                table: "candidate_career_feature_snapshots",
                columns: new[] { "candidate_id", "feature_schema_version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_work_mode_preferences_candidate_id_work_mode_id",
                table: "candidate_work_mode_preferences",
                columns: new[] { "candidate_id", "work_mode_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_candidate_work_mode_preferences_work_mode_id",
                table: "candidate_work_mode_preferences",
                column: "work_mode_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "candidate_career_feature_snapshots");

            migrationBuilder.DropTable(
                name: "candidate_work_mode_preferences");

            migrationBuilder.DropCheckConstraint(
                name: "ck_candidates_availability_days_non_negative",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "availability_days",
                table: "candidates");

            migrationBuilder.DropColumn(
                name: "professional_title",
                table: "candidates");
        }
    }
}
