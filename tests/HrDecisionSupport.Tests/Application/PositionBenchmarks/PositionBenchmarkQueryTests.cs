using HrDecisionSupport.Application.PositionBenchmarks;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Domain.Enums;

namespace HrDecisionSupport.Tests.Application.PositionBenchmarks;

[Collection(PostgreSqlIntegrationCollection.Name)]
public sealed class PositionBenchmarkQueryTests
{
    private readonly PostgreSqlIntegrationTestFixture _fixture;

    public PositionBenchmarkQueryTests(PostgreSqlIntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [PostgreSqlIntegrationFact]
    public async Task GetBenchmark_QueryCountDoesNotGrowWithEmployeePopulation()
    {
        await _fixture.ResetDatabaseAsync();
        var counter = new PostgreSqlCommandCounter();
        await using var context = _fixture.CreateDbContext(counter);
        var department = TestDatabase.Department();
        var position = TestDatabase.Position();
        position.Code = "BACKEND_DEVELOPER";
        position.Name = "Backend Developer";
        context.AddRange(department, position);
        AddEmployees(context, department, position, count: 1, offset: 0);
        await context.SaveChangesAsync();
        var service = new PositionBenchmarkService(context);

        counter.Start();
        var smallResult = await service.GetBenchmarkAsync(position.Id);
        counter.Stop();
        var smallReaderCount = counter.ReaderCount;

        AddEmployees(context, department, position, count: 9, offset: 1);
        await context.SaveChangesAsync();

        counter.Start();
        var largerResult = await service.GetBenchmarkAsync(position.Id);
        counter.Stop();
        var largerReaderCount = counter.ReaderCount;

        Assert.True(smallResult.IsSuccess);
        Assert.True(largerResult.IsSuccess);
        Assert.Equal(1, smallResult.Value.TotalEmployees);
        Assert.Equal(10, largerResult.Value.TotalEmployees);
        Assert.Equal(smallReaderCount, largerReaderCount);
    }

    private static void AddEmployees(
        Infrastructure.Persistence.HrDecisionSupportDbContext context,
        Department department,
        Position position,
        int count,
        int offset)
    {
        for (var index = 0; index < count; index++)
        {
            var sequence = index + offset;
            var person = TestDatabase.Person($"PBQ-{sequence:D3}");
            var employee = new Employee
            {
                Id = Guid.NewGuid(),
                PersonId = person.Id,
                EmployeeCode = $"PBQ-E-{sequence:D3}",
                HireDate = new DateOnly(2020, 1, 1),
                EmploymentStatus = EmploymentStatus.Active
            };
            var assignment = new EmployeeAssignment
            {
                Id = Guid.NewGuid(),
                EmployeeId = employee.Id,
                DepartmentId = department.Id,
                PositionId = position.Id,
                StartDate = new DateOnly(2020, 1, 1)
            };
            context.AddRange(person, employee, assignment);
        }
    }
}
