# hr-decision-support-system
ERP-ready HR decision support system built with ASP.NET Core MVC, Entity Framework Core and PostgreSQL.

## PostgreSQL integration tests

The normal test suite does not require PostgreSQL. To enable the opt-in integration tests, set a dedicated test-database connection string before running them. The database name must contain `test`, `integration`, or `hrds_test`; production connection strings are rejected.

```powershell
$env:HRDS_TEST_POSTGRES_CONNECTION="Host=127.0.0.1;Port=5432;Database=hrds_test;Username=postgres;Password=..."
dotnet test --filter "Category=PostgreSqlIntegration"
```

The fixture applies the existing EF migrations to that test database and resets application tables with `TRUNCATE ... RESTART IDENTITY CASCADE` between tests. It preserves `__EFMigrationsHistory`. Docker and Testcontainers are not required; a local PostgreSQL instance is sufficient. Do not use a production database or commit secrets.

## Local employee-import dry run

The dry-run runner uses only the spreadsheet reader, normalizer, and validator; it does not register a DbContext or import persistence service. It never writes import data to a database. Keep the workbook outside the repository.

```powershell
$env:HRDS_EMPLOYEE_IMPORT_FILE="C:\path\to\employee-import.xlsx"
dotnet run --project src/HrDecisionSupport.EmployeeImportDryRun -- dry-run
```

Optional: add `--observation-date 2025-01-01`. Reports are written outside the repository under `%LOCALAPPDATA%\HrDecisionSupport\employee-import-dry-run\` (`summary.json`, `diagnostics.csv`, `invalid-rows.csv`, and `warning-rows.csv`). Set `HRDS_EMPLOYEE_IMPORT_OUTPUT` to use another local directory; repository-local `artifacts-local/` is ignored if explicitly used.

## Controlled employee-import persistence runner (development/test only)

Keep the workbook outside the repository. This runner uses the existing `EmployeeImportService`; it performs a dry-run preflight, checks database reachability/migrations/model state and duplicate file hash, then requires an explicit row-count confirmation before writing anything. It never logs the connection string. Production import is intentionally locked in this runner.

1. Start a dedicated development or test PostgreSQL database and ensure migrations are already applied. Do **not** run a database update from the runner workflow.
2. Check for pending model changes:

```powershell
dotnet ef migrations has-pending-model-changes --no-build --project src/HrDecisionSupport.Infrastructure --startup-project src/HrDecisionSupport.Web --context HrDecisionSupportDbContext
```

3. Configure the workbook, a development/test connection string, and the environment explicitly:

```powershell
$env:HRDS_EMPLOYEE_IMPORT_FILE="C:\path\to\employee-import.xlsx"
$env:ConnectionStrings__PostgreSql="Host=127.0.0.1;Database=hrds_dev;Username=postgres;Password=..."
$env:ASPNETCORE_ENVIRONMENT="Development"
```

4. Run the non-persistent dry-run first:

```powershell
dotnet run --project src/HrDecisionSupport.EmployeeImportDryRun -- dry-run --observation-date 2026-08-06
```

5. Start the import preview. The interactive prompt requires the exact text shown, for example `IMPORT 6800`:

```powershell
dotnet run --project src/HrDecisionSupport.EmployeeImportDryRun -- import --observation-date 2026-08-06
```

For a non-interactive **development/test** operator run, the row count must still be explicit:

```powershell
dotnet run --project src/HrDecisionSupport.EmployeeImportDryRun -- import --observation-date 2026-08-06 --confirm-import 6800
```

The runner prints a batch-scoped verification summary after a successful import. Re-running the same byte-identical workbook is rejected by its SHA-256 file hash; it does not create a duplicate batch. Production import is not a target of this runner at this stage.
