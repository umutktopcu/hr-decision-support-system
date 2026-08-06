using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence.Migrations;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace HrDecisionSupport.Tests;

public sealed class EmployeeImportDecimalMigrationTests
{
    [Fact]
    public async Task SupportDecimalAveragePreviousCompanyStay_ChangesOnlyTargetColumnAndModelUsesNumericSixOne()
    {
        var builder = new MigrationBuilder("Npgsql.EntityFrameworkCore.PostgreSQL");
        new ExposedMigration().ApplyUp(builder);

        var operation = Assert.IsType<AlterColumnOperation>(Assert.Single(builder.Operations));
        Assert.Equal("employee_career_feature_snapshots", operation.Table); Assert.Equal("previous_company_average_stay_months", operation.Name); Assert.Equal("numeric(6,1)", operation.ColumnType); Assert.Equal(6, operation.Precision); Assert.Equal(1, operation.Scale); Assert.True(operation.IsNullable);
        await using var context = TestDatabase.CreateContext();
        var property = context.Model.FindEntityType(typeof(EmployeeCareerFeatureSnapshot))!.FindProperty(nameof(EmployeeCareerFeatureSnapshot.PreviousCompanyAverageStayMonths))!;
        Assert.Equal(typeof(decimal?), property.ClrType); Assert.Equal(6, property.GetPrecision()); Assert.Equal(1, property.GetScale());
    }

    private sealed class ExposedMigration : SupportDecimalAveragePreviousCompanyStay
    {
        public void ApplyUp(MigrationBuilder builder) => Up(builder);
    }
}
