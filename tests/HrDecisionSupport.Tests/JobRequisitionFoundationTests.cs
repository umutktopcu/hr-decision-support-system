using HrDecisionSupport.Application;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.Requisitions;
using HrDecisionSupport.Application.Requisitions.Requirements;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrDecisionSupport.Tests;

public class JobRequisitionFoundationTests
{
    [Fact]
    public void AddApplication_TimeProviderIsSingletonAndRequisitionServiceResolves()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        var rootProvider = provider.GetRequiredService<TimeProvider>();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        Assert.Same(rootProvider, firstScope.ServiceProvider.GetRequiredService<TimeProvider>());
        Assert.Same(rootProvider, secondScope.ServiceProvider.GetRequiredService<TimeProvider>());
        Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<IJobRequisitionService>());
        Assert.NotSame(
            firstScope.ServiceProvider.GetRequiredService<IJobRequisitionService>(),
            secondScope.ServiceProvider.GetRequiredService<IJobRequisitionService>());
    }

    [Fact]
    public void AddApplication_RequisitionServicesAreScopedAndExistingServicesStillResolve()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        object firstRequisition;
        object firstRequirement;
        using (var scope = provider.CreateScope())
        {
            firstRequisition = scope.ServiceProvider.GetRequiredService<IJobRequisitionService>();
            firstRequirement = scope.ServiceProvider.GetRequiredService<IJobRequisitionRequirementService>();
            Assert.Same(firstRequisition, scope.ServiceProvider.GetRequiredService<IJobRequisitionService>());
            Assert.Same(firstRequirement, scope.ServiceProvider.GetRequiredService<IJobRequisitionRequirementService>());
            Assert.NotNull(scope.ServiceProvider.GetRequiredService<IEmployeeService>());
        }
        using var secondScope = provider.CreateScope();
        Assert.NotSame(firstRequisition, secondScope.ServiceProvider.GetRequiredService<IJobRequisitionService>());
        Assert.NotSame(firstRequirement, secondScope.ServiceProvider.GetRequiredService<IJobRequisitionRequirementService>());
    }

    [Theory]
    [MemberData(nameof(ValidatorRegistrations))]
    public void AddApplication_ConcreteAndAbstractValidatorShareScopedInstance(
        Type concreteType,
        Type abstractionType)
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.Same(
            scope.ServiceProvider.GetRequiredService(concreteType),
            scope.ServiceProvider.GetRequiredService(abstractionType));
    }

    [Theory]
    [InlineData(typeof(IJobRequisitionService))]
    [InlineData(typeof(IJobRequisitionRequirementService))]
    public void ServiceContracts_UseTaskResultAndCancellationTokens(Type contractType)
    {
        foreach (var method in contractType.GetMethods())
        {
            Assert.Equal(typeof(CancellationToken), method.GetParameters().Last().ParameterType);
            Assert.True(method.ReturnType.IsGenericType);
            Assert.Equal(typeof(Task<>), method.ReturnType.GetGenericTypeDefinition());
            var taskValue = method.ReturnType.GetGenericArguments()[0];
            Assert.True(taskValue == typeof(Result)
                || taskValue.IsGenericType && taskValue.GetGenericTypeDefinition() == typeof(Result<>));
        }
        Assert.DoesNotContain(
            contractType.GetMethods(),
            method => contractType == typeof(IJobRequisitionService) && method.Name == "DeleteAsync");
    }

    [Fact]
    public void RequisitionDtos_DoNotExposeDomainEntitiesOrNavigationObjects()
    {
        var dtoTypes = new[]
        {
            typeof(JobRequisitionDto), typeof(JobRequisitionDetailDto),
            typeof(JobRequisitionRequirementDto)
        };
        foreach (var property in dtoTypes.SelectMany(type => type.GetProperties()))
        {
            Assert.False(typeof(JobRequisition).IsAssignableFrom(property.PropertyType));
            Assert.False(typeof(JobRequisitionRequirement).IsAssignableFrom(property.PropertyType));
            Assert.False(typeof(Department).IsAssignableFrom(property.PropertyType));
            Assert.False(typeof(Position).IsAssignableFrom(property.PropertyType));
            Assert.False(typeof(Competency).IsAssignableFrom(property.PropertyType));
        }
    }

    [Fact]
    public void PersistenceAbstraction_ExposesRequirementSetAsReadOnlyDbSet()
    {
        var property = typeof(IHrDecisionSupportDbContext)
            .GetProperty(nameof(IHrDecisionSupportDbContext.JobRequisitionRequirements));
        Assert.NotNull(property);
        Assert.Equal(typeof(DbSet<JobRequisitionRequirement>), property.PropertyType);
        Assert.False(property.CanWrite);
    }

    public static TheoryData<Type, Type> ValidatorRegistrations => new()
    {
        { typeof(CreateJobRequisitionRequestValidator), typeof(IValidator<CreateJobRequisitionRequest>) },
        { typeof(UpdateJobRequisitionRequestValidator), typeof(IValidator<UpdateJobRequisitionRequest>) },
        { typeof(ChangeJobRequisitionStatusRequestValidator), typeof(IValidator<ChangeJobRequisitionStatusRequest>) },
        { typeof(CreateJobRequisitionRequirementRequestValidator), typeof(IValidator<CreateJobRequisitionRequirementRequest>) },
        { typeof(UpdateJobRequisitionRequirementRequestValidator), typeof(IValidator<UpdateJobRequisitionRequirementRequest>) }
    };

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddDbContext<HrDecisionSupportDbContext>(options =>
            options.UseInMemoryDatabase($"requisition-di-{Guid.NewGuid():N}"));
        services.AddScoped<IHrDecisionSupportDbContext>(provider =>
            provider.GetRequiredService<HrDecisionSupportDbContext>());
        return services;
    }
}
