using Xunit;

namespace HrDecisionSupport.Tests;

[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class PostgreSqlIntegrationFactAttribute : FactAttribute
{
    public PostgreSqlIntegrationFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(PostgreSqlIntegrationTestFixture.ConnectionVariable)))
            Skip = "PostgreSQL integration tests are skipped because HRDS_TEST_POSTGRES_CONNECTION is not set.";
    }
}
