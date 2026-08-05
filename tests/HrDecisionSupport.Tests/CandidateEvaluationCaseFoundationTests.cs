using HrDecisionSupport.Application;
using HrDecisionSupport.Application.CandidateEvaluations;
using HrDecisionSupport.Application.Common;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.Requisitions;
using HrDecisionSupport.Domain.Entities;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrDecisionSupport.Tests;

public class CandidateEvaluationCaseFoundationTests
{
    [Fact]
    public void AddApplication_EvaluationServiceIsScopedAndTimeProviderRemainsSingleton()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        var rootTimeProvider = provider.GetRequiredService<TimeProvider>();
        object firstService;
        using (var firstScope = provider.CreateScope())
        {
            firstService = firstScope.ServiceProvider
                .GetRequiredService<ICandidateEvaluationCaseService>();
            Assert.Same(
                firstService,
                firstScope.ServiceProvider.GetRequiredService<ICandidateEvaluationCaseService>());
            Assert.Same(
                rootTimeProvider,
                firstScope.ServiceProvider.GetRequiredService<TimeProvider>());
            Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<IEmployeeService>());
            Assert.NotNull(firstScope.ServiceProvider.GetRequiredService<IJobRequisitionService>());
        }

        using var secondScope = provider.CreateScope();
        Assert.NotSame(
            firstService,
            secondScope.ServiceProvider.GetRequiredService<ICandidateEvaluationCaseService>());
        Assert.Same(
            rootTimeProvider,
            secondScope.ServiceProvider.GetRequiredService<TimeProvider>());
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

    [Fact]
    public void ServiceContract_UsesTaskResultCancellationTokensAndHasNoDelete()
    {
        var methods = typeof(ICandidateEvaluationCaseService).GetMethods();
        foreach (var method in methods)
        {
            Assert.Equal(typeof(CancellationToken), method.GetParameters().Last().ParameterType);
            Assert.True(method.ReturnType.IsGenericType);
            Assert.Equal(typeof(Task<>), method.ReturnType.GetGenericTypeDefinition());
            var taskValue = method.ReturnType.GetGenericArguments()[0];
            Assert.True(taskValue.IsGenericType
                && taskValue.GetGenericTypeDefinition() == typeof(Result<>));
        }
        Assert.DoesNotContain(methods, method => method.Name == "DeleteAsync");
    }

    [Theory]
    [InlineData(typeof(CandidateEvaluationCaseDto))]
    [InlineData(typeof(CandidateEvaluationCaseDetailDto))]
    public void EvaluationDtos_DoNotExposeSensitiveFieldsDomainEntitiesOrNavigationObjects(
        Type dtoType)
    {
        var sensitivePropertyNames = new[]
        {
            "Email",
            "EmailAddress",
            "Phone",
            "PhoneNumber",
            "Address",
            "BirthDate",
            "DateOfBirth"
        };
        var properties = dtoType.GetProperties();

        Assert.DoesNotContain(
            properties,
            property => sensitivePropertyNames.Contains(property.Name, StringComparer.Ordinal));

        foreach (var property in properties)
        {
            Assert.False(typeof(CandidateEvaluationCase).IsAssignableFrom(property.PropertyType));
            Assert.False(typeof(Candidate).IsAssignableFrom(property.PropertyType));
            Assert.False(typeof(Person).IsAssignableFrom(property.PropertyType));
            Assert.False(typeof(JobRequisition).IsAssignableFrom(property.PropertyType));
            Assert.False(typeof(Department).IsAssignableFrom(property.PropertyType));
            Assert.False(typeof(Position).IsAssignableFrom(property.PropertyType));
        }
    }

    [Theory]
    [InlineData(nameof(IHrDecisionSupportDbContext.CandidateEvaluationCases), typeof(DbSet<CandidateEvaluationCase>))]
    [InlineData(nameof(IHrDecisionSupportDbContext.Candidates), typeof(DbSet<Candidate>))]
    [InlineData(nameof(IHrDecisionSupportDbContext.People), typeof(DbSet<Person>))]
    [InlineData(nameof(IHrDecisionSupportDbContext.JobRequisitions), typeof(DbSet<JobRequisition>))]
    public void PersistenceAbstraction_ExposesRequiredReadOnlySets(string propertyName, Type setType)
    {
        var property = typeof(IHrDecisionSupportDbContext).GetProperty(propertyName);
        Assert.NotNull(property);
        Assert.Equal(setType, property.PropertyType);
        Assert.False(property.CanWrite);
    }

    public static TheoryData<Type, Type> ValidatorRegistrations => new()
    {
        {
            typeof(CreateCandidateEvaluationCaseRequestValidator),
            typeof(IValidator<CreateCandidateEvaluationCaseRequest>)
        },
        {
            typeof(UpdateCandidateEvaluationCaseRequestValidator),
            typeof(IValidator<UpdateCandidateEvaluationCaseRequest>)
        },
        {
            typeof(ChangeCandidateEvaluationCaseStatusRequestValidator),
            typeof(IValidator<ChangeCandidateEvaluationCaseStatusRequest>)
        }
    };

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddDbContext<HrDecisionSupportDbContext>(options =>
            options.UseInMemoryDatabase($"evaluation-di-{Guid.NewGuid():N}"));
        services.AddScoped<IHrDecisionSupportDbContext>(provider =>
            provider.GetRequiredService<HrDecisionSupportDbContext>());
        return services;
    }
}
