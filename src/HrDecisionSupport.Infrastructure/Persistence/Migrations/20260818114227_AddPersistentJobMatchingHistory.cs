using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrDecisionSupport.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistentJobMatchingHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_matching_runs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_requisition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    executed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    job_requisition_code_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    job_title_snapshot = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    retrieval_top_n = table.Column<int>(type: "integer", nullable: false),
                    final_top_n = table.Column<int>(type: "integer", nullable: false),
                    candidate_pool_count = table.Column<int>(type: "integer", nullable: false),
                    hard_filter_passed_count = table.Column<int>(type: "integer", nullable: false),
                    retrieved_candidate_count = table.Column<int>(type: "integer", nullable: false),
                    final_candidate_count = table.Column<int>(type: "integer", nullable: false),
                    embedding_model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    reranker_model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    retention_model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    retention_feature_schema_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    job_document_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    configuration_snapshot_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_matching_runs", x => x.id);
                    table.CheckConstraint("ck_job_matching_runs_counts_non_negative", "candidate_pool_count >= 0 AND hard_filter_passed_count >= 0 AND retrieved_candidate_count >= 0 AND final_candidate_count >= 0");
                    table.CheckConstraint("ck_job_matching_runs_top_n_positive", "retrieval_top_n > 0 AND final_top_n > 0");
                    table.ForeignKey(
                        name: "FK_job_matching_runs_job_requisitions_job_requisition_id",
                        column: x => x.job_requisition_id,
                        principalTable: "job_requisitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "job_matching_run_results",
                columns: table => new
                {
                    job_matching_run_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    candidate_code_snapshot = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    candidate_display_name_snapshot = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    final_rank = table.Column<int>(type: "integer", nullable: false),
                    skill_tier = table.Column<int>(type: "integer", nullable: false),
                    mandatory_skill_coverage = table.Column<decimal>(type: "numeric", nullable: false),
                    preferred_skill_coverage = table.Column<decimal>(type: "numeric", nullable: false),
                    embedding_score = table.Column<double>(type: "double precision", nullable: false),
                    cross_encoder_raw_score = table.Column<double>(type: "double precision", nullable: false),
                    job_fit_score = table.Column<double>(type: "double precision", nullable: false),
                    retention_prediction_status = table.Column<int>(type: "integer", nullable: false),
                    retention_label = table.Column<int>(type: "integer", nullable: true),
                    shortest_previous_job_months_snapshot = table.Column<int>(type: "integer", nullable: true),
                    longest_previous_job_months_snapshot = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_matching_run_results", x => new { x.job_matching_run_id, x.candidate_id });
                    table.CheckConstraint("ck_job_matching_run_results_coverage_range", "mandatory_skill_coverage >= 0 AND mandatory_skill_coverage <= 1 AND preferred_skill_coverage >= 0 AND preferred_skill_coverage <= 1");
                    table.CheckConstraint("ck_job_matching_run_results_months_non_negative", "(shortest_previous_job_months_snapshot IS NULL OR shortest_previous_job_months_snapshot >= 0) AND (longest_previous_job_months_snapshot IS NULL OR longest_previous_job_months_snapshot >= 0)");
                    table.CheckConstraint("ck_job_matching_run_results_rank_tier_positive", "final_rank > 0 AND skill_tier > 0");
                    table.CheckConstraint("ck_job_matching_run_results_retention_label_valid", "retention_label IS NULL OR retention_label IN (0, 1, 2)");
                    table.CheckConstraint("ck_job_matching_run_results_retention_status_valid", "retention_prediction_status IN (1, 2, 3)");
                    table.CheckConstraint("ck_job_matching_run_results_shortest_lte_longest", "shortest_previous_job_months_snapshot IS NULL OR longest_previous_job_months_snapshot IS NULL OR shortest_previous_job_months_snapshot <= longest_previous_job_months_snapshot");
                    table.ForeignKey(
                        name: "FK_job_matching_run_results_job_matching_runs_job_matching_run~",
                        column: x => x.job_matching_run_id,
                        principalTable: "job_matching_runs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_matching_run_results_job_matching_run_id_final_rank",
                table: "job_matching_run_results",
                columns: new[] { "job_matching_run_id", "final_rank" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_job_matching_runs_job_requisition_id_executed_at_utc",
                table: "job_matching_runs",
                columns: new[] { "job_requisition_id", "executed_at_utc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_matching_run_results");

            migrationBuilder.DropTable(
                name: "job_matching_runs");
        }
    }
}
