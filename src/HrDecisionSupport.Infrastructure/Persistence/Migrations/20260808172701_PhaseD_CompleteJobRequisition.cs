using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrDecisionSupport.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PhaseD_CompleteJobRequisition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_job_requisitions_overall_skill_threshold",
                table: "job_requisitions");

            migrationBuilder.DropColumn(
                name: "overall_skill_coverage_threshold",
                table: "job_requisitions");

            migrationBuilder.AddColumn<int>(
                name: "minimum_education_level",
                table: "job_requisitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "work_mode_hard_filter_enabled",
                table: "job_requisitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "work_mode_id",
                table: "job_requisitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "job_language_requirements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_requisition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    language_id = table.Column<Guid>(type: "uuid", nullable: false),
                    minimum_proficiency = table.Column<int>(type: "integer", nullable: false),
                    hard_filter_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_job_language_requirements", x => x.id);
                    table.ForeignKey(
                        name: "FK_job_language_requirements_job_requisitions_job_requisition_~",
                        column: x => x.job_requisition_id,
                        principalTable: "job_requisitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_job_language_requirements_languages_language_id",
                        column: x => x.language_id,
                        principalTable: "languages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_job_requisitions_work_mode_id",
                table: "job_requisitions",
                column: "work_mode_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_language_requirements_job_requisition_id",
                table: "job_language_requirements",
                column: "job_requisition_id");

            migrationBuilder.CreateIndex(
                name: "IX_job_language_requirements_language_id",
                table: "job_language_requirements",
                column: "language_id");

            migrationBuilder.AddForeignKey(
                name: "FK_job_requisitions_work_modes_work_mode_id",
                table: "job_requisitions",
                column: "work_mode_id",
                principalTable: "work_modes",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_job_requisitions_work_modes_work_mode_id",
                table: "job_requisitions");

            migrationBuilder.DropTable(
                name: "job_language_requirements");

            migrationBuilder.DropIndex(
                name: "IX_job_requisitions_work_mode_id",
                table: "job_requisitions");

            migrationBuilder.DropColumn(
                name: "minimum_education_level",
                table: "job_requisitions");

            migrationBuilder.DropColumn(
                name: "work_mode_hard_filter_enabled",
                table: "job_requisitions");

            migrationBuilder.DropColumn(
                name: "work_mode_id",
                table: "job_requisitions");

            migrationBuilder.AddColumn<decimal>(
                name: "overall_skill_coverage_threshold",
                table: "job_requisitions",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_requisitions_overall_skill_threshold",
                table: "job_requisitions",
                sql: "overall_skill_coverage_threshold IS NULL OR (overall_skill_coverage_threshold >= 0 AND overall_skill_coverage_threshold <= 1)");
        }
    }
}
