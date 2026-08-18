using HrDecisionSupport.Application;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Infrastructure;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Pgvector.EntityFrameworkCore;

namespace HrDecisionSupport.Tests;

public sealed class PostgreSqlIntegrationTestFixture : IAsyncLifetime
{
    public const string ConnectionVariable = "HRDS_TEST_POSTGRES_CONNECTION";
    private const string MissingConnectionMessage = "PostgreSQL integration tests are skipped because HRDS_TEST_POSTGRES_CONNECTION is not set.";
    private readonly string? _connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_connectionString);
    public string ConnectionString => IsConfigured ? _connectionString! : throw new InvalidOperationException(MissingConnectionMessage);

    public async Task InitializeAsync()
    {
        if (!IsConfigured) return;
        ValidateTestDatabase(ConnectionString);
        await using var context = CreateDbContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public HrDecisionSupportDbContext CreateDbContext(DbCommandInterceptor? interceptor = null)
    {
        RequireConfigured();
        var builder = new DbContextOptionsBuilder<HrDecisionSupportDbContext>()
            .UseNpgsql(ConnectionString, npgsqlOptions => npgsqlOptions.UseVector());
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        var options = builder.Options;
        return new HrDecisionSupportDbContext(options);
    }

    public ServiceProvider CreateServiceProvider(TimeProvider? timeProvider = null, IEmployeeSpreadsheetReader? spreadsheetReader = null)
    {
        RequireConfigured();
        var services = new ServiceCollection();
        if (timeProvider is not null) services.AddSingleton(timeProvider);
        services.AddApplication();
        services.AddInfrastructure(options =>
            options.UseNpgsql(ConnectionString, npgsqlOptions => npgsqlOptions.UseVector()));
        if (spreadsheetReader is not null)
        {
            services.RemoveAll<IEmployeeSpreadsheetReader>();
            services.AddSingleton(spreadsheetReader);
        }
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
    }

    public async Task ResetDatabaseAsync()
    {
        RequireConfigured();
        await using var context = CreateDbContext();
        var tables = context.Model.GetEntityTypes()
            .Select(entity => new { Schema = entity.GetSchema() ?? "public", Table = entity.GetTableName() })
            .Where(item => item.Table is not null && string.Equals(item.Schema, "public", StringComparison.Ordinal))
            .Select(item => Quote(item.Schema) + "." + Quote(item.Table!))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (tables.Length != 0)
        {
            var resetSql = "TRUNCATE TABLE " + string.Join(", ", tables) + " RESTART IDENTITY CASCADE;";
            await context.Database.ExecuteSqlRawAsync(resetSql);
        }
    }

    public async Task<int> GetMigrationHistoryCountAsync()
    {
        RequireConfigured();
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("SELECT COUNT(*) FROM \"__EFMigrationsHistory\";", connection);
        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    public void RequireConfigured()
    {
        if (!IsConfigured) throw new InvalidOperationException(MissingConnectionMessage);
    }

    public static void ValidateTestDatabase(string connectionString)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        var database = builder.Database;
        if (string.IsNullOrWhiteSpace(database) || !(database.Contains("test", StringComparison.OrdinalIgnoreCase) || database.Contains("integration", StringComparison.OrdinalIgnoreCase) || database.Contains("hrds_test", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("HRDS_TEST_POSTGRES_CONNECTION must target a database whose name contains test, integration, or hrds_test.");
    }

    private static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
}
