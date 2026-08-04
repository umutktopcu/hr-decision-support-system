using HrDecisionSupport.Application;
using HrDecisionSupport.Application.Candidates;
using HrDecisionSupport.Application.Candidates.Dtos;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.Employees.Dtos;
using HrDecisionSupport.Infrastructure;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrDecisionSupport.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_ResolvesServicesAndValidators()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<EmployeeService>(
            scope.ServiceProvider.GetRequiredService<IEmployeeService>());
        Assert.IsType<CandidateService>(
            scope.ServiceProvider.GetRequiredService<ICandidateService>());
        Assert.IsType<CreateEmployeeRequestValidator>(scope.ServiceProvider
            .GetRequiredService<IValidator<CreateEmployeeRequest>>());
        Assert.IsType<UpdateEmployeeRequestValidator>(scope.ServiceProvider
            .GetRequiredService<IValidator<UpdateEmployeeRequest>>());
        Assert.IsType<CreateCandidateRequestValidator>(scope.ServiceProvider
            .GetRequiredService<IValidator<CreateCandidateRequest>>());
        Assert.IsType<UpdateCandidateRequestValidator>(scope.ServiceProvider
            .GetRequiredService<IValidator<UpdateCandidateRequest>>());
    }

    [Fact]
    public void AddInfrastructure_MapsAbstractionAndConcreteToSameScopedInstance()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var concrete = scope.ServiceProvider.GetRequiredService<HrDecisionSupportDbContext>();
        var abstraction = scope.ServiceProvider.GetRequiredService<IHrDecisionSupportDbContext>();

        Assert.Same(concrete, abstraction);
    }

    [Fact]
    public async Task ApplicationServices_UseContextRegisteredInSameScope()
    {
        var services = CreateServices();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<HrDecisionSupportDbContext>();
        context.People.Add(TestDatabase.Person("ANON-01"));
        await context.SaveChangesAsync();

        var employeeResult = await scope.ServiceProvider
            .GetRequiredService<IEmployeeService>().ListAsync();
        var candidateResult = await scope.ServiceProvider
            .GetRequiredService<ICandidateService>().ListAsync();

        Assert.True(employeeResult.IsSuccess);
        Assert.True(candidateResult.IsSuccess);
        Assert.Same(
            context,
            scope.ServiceProvider.GetRequiredService<IHrDecisionSupportDbContext>());
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(options =>
            options.UseInMemoryDatabase($"di-{Guid.NewGuid():N}"));
        return services;
    }
}
