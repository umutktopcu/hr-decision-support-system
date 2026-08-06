# hr-decision-support-system
ERP-ready HR decision support system built with ASP.NET Core MVC, Entity Framework Core and PostgreSQL.

## PostgreSQL integration tests

The normal test suite does not require PostgreSQL. To enable the opt-in integration tests, set a dedicated test-database connection string before running them. The database name must contain `test`, `integration`, or `hrds_test`; production connection strings are rejected.

```powershell
$env:HRDS_TEST_POSTGRES_CONNECTION="Host=127.0.0.1;Port=5432;Database=hrds_test;Username=postgres;Password=..."
dotnet test --filter "Category=PostgreSqlIntegration"
```

The fixture applies the existing EF migrations to that test database and resets application tables with `TRUNCATE ... RESTART IDENTITY CASCADE` between tests. It preserves `__EFMigrationsHistory`. Docker and Testcontainers are not required; a local PostgreSQL instance is sufficient. Do not use a production database or commit secrets.
