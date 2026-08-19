using HrDecisionSupport.Application;
using HrDecisionSupport.Application.Candidates;
using HrDecisionSupport.Application.Candidates.Dtos;
using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.Employees.Dtos;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Application.Profiles.Certificates;
using HrDecisionSupport.Application.Profiles.Competencies;
using HrDecisionSupport.Application.Profiles.Education;
using HrDecisionSupport.Application.Profiles.EmploymentHistory;
using HrDecisionSupport.Application.Profiles.Languages;
using HrDecisionSupport.Application.Profiles.Projects;
using HrDecisionSupport.Application.Profiles.Sectors;
using HrDecisionSupport.Application.Profiles.WorkModes;
using HrDecisionSupport.Application.PositionBenchmarks;
using HrDecisionSupport.Infrastructure;
using HrDecisionSupport.Infrastructure.Persistence;
using HrDecisionSupport.Infrastructure.EmployeeImports;
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
        Assert.IsType<PersonCompetencyService>(scope.ServiceProvider.GetRequiredService<IPersonCompetencyService>());
        Assert.IsType<EducationRecordService>(scope.ServiceProvider.GetRequiredService<IEducationRecordService>());
        Assert.IsType<PersonCertificateService>(scope.ServiceProvider.GetRequiredService<IPersonCertificateService>());
        Assert.IsType<PersonLanguageService>(scope.ServiceProvider.GetRequiredService<IPersonLanguageService>());
        Assert.IsType<EmploymentHistoryService>(scope.ServiceProvider.GetRequiredService<IEmploymentHistoryService>());
        Assert.IsType<PersonProjectService>(scope.ServiceProvider.GetRequiredService<IPersonProjectService>());
        Assert.IsType<PersonSectorExperienceService>(scope.ServiceProvider.GetRequiredService<IPersonSectorExperienceService>());
        Assert.IsType<PersonWorkModeExperienceService>(scope.ServiceProvider.GetRequiredService<IPersonWorkModeExperienceService>());
        Assert.IsType<PositionBenchmarkService>(scope.ServiceProvider.GetRequiredService<IPositionBenchmarkService>());
        AssertValidator<CreatePersonCompetencyRequest, CreatePersonCompetencyRequestValidator>(scope.ServiceProvider);
        AssertValidator<UpdatePersonCompetencyRequest, UpdatePersonCompetencyRequestValidator>(scope.ServiceProvider);
        AssertValidator<CreateEducationRecordRequest, CreateEducationRecordRequestValidator>(scope.ServiceProvider);
        AssertValidator<UpdateEducationRecordRequest, UpdateEducationRecordRequestValidator>(scope.ServiceProvider);
        AssertValidator<CreatePersonCertificateRequest, CreatePersonCertificateRequestValidator>(scope.ServiceProvider);
        AssertValidator<UpdatePersonCertificateRequest, UpdatePersonCertificateRequestValidator>(scope.ServiceProvider);
        AssertValidator<CreatePersonLanguageRequest, CreatePersonLanguageRequestValidator>(scope.ServiceProvider);
        AssertValidator<UpdatePersonLanguageRequest, UpdatePersonLanguageRequestValidator>(scope.ServiceProvider);
        AssertValidator<CreateEmploymentHistoryRequest, CreateEmploymentHistoryRequestValidator>(scope.ServiceProvider);
        AssertValidator<UpdateEmploymentHistoryRequest, UpdateEmploymentHistoryRequestValidator>(scope.ServiceProvider);
        AssertValidator<CreatePersonProjectRequest, CreatePersonProjectRequestValidator>(scope.ServiceProvider);
        AssertValidator<UpdatePersonProjectRequest, UpdatePersonProjectRequestValidator>(scope.ServiceProvider);
        AssertValidator<CreatePersonSectorExperienceRequest, CreatePersonSectorExperienceRequestValidator>(scope.ServiceProvider);
        AssertValidator<UpdatePersonSectorExperienceRequest, UpdatePersonSectorExperienceRequestValidator>(scope.ServiceProvider);
        AssertValidator<CreatePersonWorkModeExperienceRequest, CreatePersonWorkModeExperienceRequestValidator>(scope.ServiceProvider);
        AssertValidator<UpdatePersonWorkModeExperienceRequest, UpdatePersonWorkModeExperienceRequestValidator>(scope.ServiceProvider);
    }

    [Theory]
    [InlineData(typeof(IPersonCompetencyService))]
    [InlineData(typeof(IEducationRecordService))]
    [InlineData(typeof(IPersonCertificateService))]
    [InlineData(typeof(IPersonLanguageService))]
    [InlineData(typeof(IEmploymentHistoryService))]
    [InlineData(typeof(IPersonProjectService))]
    [InlineData(typeof(IPersonSectorExperienceService))]
    [InlineData(typeof(IPersonWorkModeExperienceService))]
    public void ProfileServices_AreScoped(Type serviceType)
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredService(serviceType);
        var sameScope = firstScope.ServiceProvider.GetRequiredService(serviceType);
        var differentScope = secondScope.ServiceProvider.GetRequiredService(serviceType);

        Assert.NotNull(first);
        Assert.Same(first, sameScope);
        Assert.NotSame(first, differentScope);
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
    public void AddInfrastructure_ResolvesStatelessSpreadsheetReader()
    {
        var services = CreateServices();
        using var provider = services.BuildServiceProvider();

        var first = provider.GetRequiredService<IEmployeeSpreadsheetReader>();
        var second = provider.GetRequiredService<IEmployeeSpreadsheetReader>();

        Assert.IsType<ClosedXmlEmployeeSpreadsheetReader>(first);
        Assert.Same(first, second);
        Assert.Empty(Assert.Single(typeof(ClosedXmlEmployeeSpreadsheetReader)
            .GetConstructors()).GetParameters());
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

    private static void AssertValidator<TRequest, TValidator>(IServiceProvider provider)
        where TValidator : class, IValidator<TRequest>
    {
        var concrete = Assert.IsType<TValidator>(provider.GetRequiredService<TValidator>());
        var abstraction = Assert.IsType<TValidator>(provider.GetRequiredService<IValidator<TRequest>>());
        Assert.Same(concrete, abstraction);
    }
}
