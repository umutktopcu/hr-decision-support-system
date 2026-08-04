using HrDecisionSupport.Application.Candidates;
using HrDecisionSupport.Application.Candidates.Dtos;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.Employees.Dtos;
using HrDecisionSupport.Application.Profiles.Certificates;
using HrDecisionSupport.Application.Profiles.Competencies;
using HrDecisionSupport.Application.Profiles.Education;
using HrDecisionSupport.Application.Profiles.Languages;
using Microsoft.Extensions.DependencyInjection;

namespace HrDecisionSupport.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ICandidateService, CandidateService>();
        services.AddScoped<IPersonCompetencyService, PersonCompetencyService>();
        services.AddScoped<IEducationRecordService, EducationRecordService>();
        services.AddScoped<IPersonCertificateService, PersonCertificateService>();
        services.AddScoped<IPersonLanguageService, PersonLanguageService>();

        services.AddScoped<CreateEmployeeRequestValidator>();
        services.AddScoped<IValidator<CreateEmployeeRequest>>(serviceProvider =>
            serviceProvider.GetRequiredService<CreateEmployeeRequestValidator>());
        services.AddScoped<UpdateEmployeeRequestValidator>();
        services.AddScoped<IValidator<UpdateEmployeeRequest>>(serviceProvider =>
            serviceProvider.GetRequiredService<UpdateEmployeeRequestValidator>());
        services.AddScoped<CreateCandidateRequestValidator>();
        services.AddScoped<IValidator<CreateCandidateRequest>>(serviceProvider =>
            serviceProvider.GetRequiredService<CreateCandidateRequestValidator>());
        services.AddScoped<UpdateCandidateRequestValidator>();
        services.AddScoped<IValidator<UpdateCandidateRequest>>(serviceProvider =>
            serviceProvider.GetRequiredService<UpdateCandidateRequestValidator>());

        AddValidator<CreatePersonCompetencyRequest, CreatePersonCompetencyRequestValidator>(services);
        AddValidator<UpdatePersonCompetencyRequest, UpdatePersonCompetencyRequestValidator>(services);
        AddValidator<CreateEducationRecordRequest, CreateEducationRecordRequestValidator>(services);
        AddValidator<UpdateEducationRecordRequest, UpdateEducationRecordRequestValidator>(services);
        AddValidator<CreatePersonCertificateRequest, CreatePersonCertificateRequestValidator>(services);
        AddValidator<UpdatePersonCertificateRequest, UpdatePersonCertificateRequestValidator>(services);
        AddValidator<CreatePersonLanguageRequest, CreatePersonLanguageRequestValidator>(services);
        AddValidator<UpdatePersonLanguageRequest, UpdatePersonLanguageRequestValidator>(services);

        return services;
    }

    private static void AddValidator<TRequest, TValidator>(IServiceCollection services)
        where TValidator : class, IValidator<TRequest>
    {
        services.AddScoped<TValidator>();
        services.AddScoped<IValidator<TRequest>>(serviceProvider =>
            serviceProvider.GetRequiredService<TValidator>());
    }
}
