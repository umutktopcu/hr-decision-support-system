using HrDecisionSupport.Application;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.Employees.Assignments;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrDecisionSupport.Tests;

public class EmployeeAssignmentFoundationTests
{
    [Fact]
    public void AddApplication_EmployeeAssignmentServiceIsScoped()
    {
        using var provider = CreateProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService<IEmployeeAssignmentService>();
        var firstAgain = firstScope.ServiceProvider.GetRequiredService<IEmployeeAssignmentService>();
        var second = secondScope.ServiceProvider.GetRequiredService<IEmployeeAssignmentService>();

        Assert.IsType<EmployeeAssignmentService>(first);
        Assert.Same(first, firstAgain);
        Assert.NotSame(first, second);
    }

    [Fact]
    public void AddApplication_AssignmentValidatorsResolveAsSharedScopedInstances()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;

        AssertValidator<CreateEmployeeAssignmentRequest, CreateEmployeeAssignmentRequestValidator>(services);
        AssertValidator<UpdateEmployeeAssignmentRequest, UpdateEmployeeAssignmentRequestValidator>(services);
        AssertValidator<ChangeCurrentEmployeeAssignmentRequest,
            ChangeCurrentEmployeeAssignmentRequestValidator>(services);
        AssertValidator<CloseEmployeeAssignmentRequest, CloseEmployeeAssignmentRequestValidator>(services);
    }

    [Fact]
    public void EmployeeAssignmentServiceContract_HasExpectedAsyncResultMethods()
    {
        var methods = typeof(IEmployeeAssignmentService).GetMethods();

        Assert.Equal(
            ["ChangeCurrentAsync", "CloseAsync", "CreateAsync", "GetByIdAsync", "ListByEmployeeAsync", "UpdateAsync"],
            methods.Select(method => method.Name).Order().ToArray());
        Assert.All(methods, method =>
        {
            Assert.Equal(typeof(CancellationToken), method.GetParameters().Last().ParameterType);
            Assert.True(method.ReturnType.IsGenericType);
            Assert.Equal(typeof(Task<>), method.ReturnType.GetGenericTypeDefinition());
            var resultType = method.ReturnType.GetGenericArguments()[0];
            Assert.True(resultType.IsGenericType);
            Assert.Equal(typeof(Result<>), resultType.GetGenericTypeDefinition());
        });
        Assert.DoesNotContain(methods, method => method.Name.Contains("Delete", StringComparison.Ordinal));
    }

    [Fact]
    public void EmployeeAssignmentDto_DoesNotExposeDomainOrNavigationObjects()
    {
        Assert.All(typeof(EmployeeAssignmentDto).GetProperties(), property =>
        {
            Assert.False(typeof(EmployeeAssignment).IsAssignableFrom(property.PropertyType));
            Assert.False(property.PropertyType.Namespace == typeof(EmployeeAssignment).Namespace);
        });
    }

    [Fact]
    public void AddApplication_ExistingEmployeeServiceStillResolves()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<EmployeeService>(scope.ServiceProvider.GetRequiredService<IEmployeeService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEmployeeAssignmentService>());
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddInfrastructure(options => options.UseInMemoryDatabase($"assignment-di-{Guid.NewGuid():N}"));
        return services.BuildServiceProvider();
    }

    private static void AssertValidator<TRequest, TValidator>(IServiceProvider provider)
        where TValidator : class, IValidator<TRequest>
    {
        var concrete = provider.GetRequiredService<TValidator>();
        var abstraction = provider.GetRequiredService<IValidator<TRequest>>();
        Assert.Same(concrete, abstraction);
    }
}
