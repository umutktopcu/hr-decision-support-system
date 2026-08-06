namespace HrDecisionSupport.Tests;

public sealed class PostgreSqlIntegrationSafetyTests
{
    [Fact]
    public void ValidateTestDatabase_RejectsProductionLikeDatabaseName()
    {
        Assert.Throws<InvalidOperationException>(() => PostgreSqlIntegrationTestFixture.ValidateTestDatabase("Host=127.0.0.1;Database=hrds_production;Username=postgres;Password=secret"));
    }
}
