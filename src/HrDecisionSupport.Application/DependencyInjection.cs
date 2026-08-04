using HrDecisionSupport.Application.Candidates;
using HrDecisionSupport.Application.Candidates.Dtos;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.Employees.Dtos;
using Microsoft.Extensions.DependencyInjection;

namespace HrDecisionSupport.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<ICandidateService, CandidateService>();

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

        return services;
    }
}
