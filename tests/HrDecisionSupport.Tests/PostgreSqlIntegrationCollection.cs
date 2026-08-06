using Xunit;

namespace HrDecisionSupport.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlIntegrationCollection : ICollectionFixture<PostgreSqlIntegrationTestFixture>
{
    public const string Name = "PostgreSqlIntegration";
}
