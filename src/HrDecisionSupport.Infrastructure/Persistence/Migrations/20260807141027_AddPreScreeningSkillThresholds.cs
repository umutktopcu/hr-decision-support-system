using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrDecisionSupport.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPreScreeningSkillThresholds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "mandatory_skill_coverage_threshold",
                table: "job_requisitions",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "overall_skill_coverage_threshold",
                table: "job_requisitions",
                type: "numeric(5,4)",
                precision: 5,
                scale: 4,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_requisitions_mandatory_skill_threshold",
                table: "job_requisitions",
                sql: "mandatory_skill_coverage_threshold IS NULL OR (mandatory_skill_coverage_threshold >= 0 AND mandatory_skill_coverage_threshold <= 1)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_requisitions_overall_skill_threshold",
                table: "job_requisitions",
                sql: "overall_skill_coverage_threshold IS NULL OR (overall_skill_coverage_threshold >= 0 AND overall_skill_coverage_threshold <= 1)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_job_requisitions_mandatory_skill_threshold",
                table: "job_requisitions");

            migrationBuilder.DropCheckConstraint(
                name: "ck_job_requisitions_overall_skill_threshold",
                table: "job_requisitions");

            migrationBuilder.DropColumn(
                name: "mandatory_skill_coverage_threshold",
                table: "job_requisitions");

            migrationBuilder.DropColumn(
                name: "overall_skill_coverage_threshold",
                table: "job_requisitions");
        }
    }
}
