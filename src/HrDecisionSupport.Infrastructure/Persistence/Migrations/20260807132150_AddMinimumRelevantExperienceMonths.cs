using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrDecisionSupport.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMinimumRelevantExperienceMonths : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "minimum_relevant_experience_months",
                table: "job_requisitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_job_requisitions_min_relevant_experience",
                table: "job_requisitions",
                sql: "minimum_relevant_experience_months IS NULL OR minimum_relevant_experience_months >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_job_requisitions_min_relevant_experience",
                table: "job_requisitions");

            migrationBuilder.DropColumn(
                name: "minimum_relevant_experience_months",
                table: "job_requisitions");
        }
    }
}
