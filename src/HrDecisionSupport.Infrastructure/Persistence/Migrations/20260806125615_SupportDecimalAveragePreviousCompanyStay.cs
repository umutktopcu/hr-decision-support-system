using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HrDecisionSupport.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SupportDecimalAveragePreviousCompanyStay : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "previous_company_average_stay_months",
                table: "employee_career_feature_snapshots",
                type: "numeric(6,1)",
                precision: 6,
                scale: 1,
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Converting numeric(6,1) back to integer is inherently lossy for fractional values.
            // Keep the provider-generated conversion uncustomized: no silent rounding policy is introduced here.
            migrationBuilder.AlterColumn<int>(
                name: "previous_company_average_stay_months",
                table: "employee_career_feature_snapshots",
                type: "integer",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(6,1)",
                oldPrecision: 6,
                oldScale: 1,
                oldNullable: true);
        }
    }
}
