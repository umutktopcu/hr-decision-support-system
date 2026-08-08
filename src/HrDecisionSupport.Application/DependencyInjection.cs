using HrDecisionSupport.Application.Candidates;
using HrDecisionSupport.Application.Candidates.Dtos;
using HrDecisionSupport.Application.CandidateEvaluations;
using HrDecisionSupport.Application.Common.Validation;
using HrDecisionSupport.Application.Employees;
using HrDecisionSupport.Application.Employees.Dtos;
using HrDecisionSupport.Application.Employees.Assignments;
using HrDecisionSupport.Application.EmployeeImports.Processing;
using HrDecisionSupport.Application.EmployeeImports.Persistence;
using HrDecisionSupport.Application.Profiles.Certificates;
using HrDecisionSupport.Application.Profiles.Competencies;
using HrDecisionSupport.Application.Profiles.Education;
using HrDecisionSupport.Application.Profiles.EmploymentHistory;
using HrDecisionSupport.Application.Profiles.Languages;
using HrDecisionSupport.Application.Profiles.Projects;
using HrDecisionSupport.Application.Profiles.Sectors;
using HrDecisionSupport.Application.Profiles.WorkModes;
using HrDecisionSupport.Application.CandidateImports;
using HrDecisionSupport.Application.Requisitions;
using HrDecisionSupport.Application.Requisitions.Requirements;
using HrDecisionSupport.Application.PreScreening;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace HrDecisionSupport.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddSingleton<IEmployeeImportRowNormalizer, EmployeeImportRowNormalizer>();
        services.AddSingleton<IEmployeeImportRowValidator, EmployeeImportRowValidator>();
        services.AddSingleton<IEmployeeImportDryRunService, EmployeeImportDryRunService>();
        services.AddSingleton<IEmployeeImportRowPersistenceHook, NoOpEmployeeImportRowPersistenceHook>();
        services.AddSingleton<IEmployeeImportOrchestrationHook, NoOpEmployeeImportOrchestrationHook>();
        services.AddScoped<IEmployeeImportService, EmployeeImportService>();

        services.AddScoped<IEmployeeService, EmployeeService>();
        services.AddScoped<IEmployeeAssignmentService, EmployeeAssignmentService>();
        services.AddScoped<ICandidateService, CandidateService>();
        services.AddScoped<IPersonCompetencyService, PersonCompetencyService>();
        services.AddScoped<IEducationRecordService, EducationRecordService>();
        services.AddScoped<IPersonCertificateService, PersonCertificateService>();
        services.AddScoped<IPersonLanguageService, PersonLanguageService>();
        services.AddScoped<IEmploymentHistoryService, EmploymentHistoryService>();
        services.AddScoped<IPersonProjectService, PersonProjectService>();
        services.AddScoped<IPersonSectorExperienceService, PersonSectorExperienceService>();
        services.AddScoped<IPersonWorkModeExperienceService, PersonWorkModeExperienceService>();
        services.AddScoped<IJobRequisitionService, JobRequisitionService>();
        services.AddScoped<IJobRequisitionRequirementService, JobRequisitionRequirementService>();
        services.AddScoped<ICandidateEvaluationCaseService, CandidateEvaluationCaseService>();
        services.AddScoped<CandidateImportService>();
        services.AddScoped<ICandidatePreScreeningService, CandidatePreScreeningService>();
        services.AddScoped<HrDecisionSupport.Application.SemanticMatching.Orchestration.ICandidateSemanticMatchingService, HrDecisionSupport.Application.SemanticMatching.Orchestration.CandidateSemanticMatchingService>();
        services.AddScoped<HrDecisionSupport.Application.SemanticMatching.Documents.CandidateDocumentBuilder>();
        services.AddScoped<HrDecisionSupport.Application.SemanticMatching.Documents.JobDocumentBuilder>();

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
        AddValidator<CreateEmploymentHistoryRequest, CreateEmploymentHistoryRequestValidator>(services);
        AddValidator<UpdateEmploymentHistoryRequest, UpdateEmploymentHistoryRequestValidator>(services);
        AddValidator<CreatePersonProjectRequest, CreatePersonProjectRequestValidator>(services);
        AddValidator<UpdatePersonProjectRequest, UpdatePersonProjectRequestValidator>(services);
        AddValidator<CreatePersonSectorExperienceRequest, CreatePersonSectorExperienceRequestValidator>(services);
        AddValidator<UpdatePersonSectorExperienceRequest, UpdatePersonSectorExperienceRequestValidator>(services);
        AddValidator<CreatePersonWorkModeExperienceRequest, CreatePersonWorkModeExperienceRequestValidator>(services);
        AddValidator<UpdatePersonWorkModeExperienceRequest, UpdatePersonWorkModeExperienceRequestValidator>(services);
        AddValidator<CreateEmployeeAssignmentRequest, CreateEmployeeAssignmentRequestValidator>(services);
        AddValidator<UpdateEmployeeAssignmentRequest, UpdateEmployeeAssignmentRequestValidator>(services);
        AddValidator<ChangeCurrentEmployeeAssignmentRequest, ChangeCurrentEmployeeAssignmentRequestValidator>(services);
        AddValidator<CloseEmployeeAssignmentRequest, CloseEmployeeAssignmentRequestValidator>(services);
        AddValidator<CreateJobRequisitionRequest, CreateJobRequisitionRequestValidator>(services);
        AddValidator<UpdateJobRequisitionRequest, UpdateJobRequisitionRequestValidator>(services);
        AddValidator<ChangeJobRequisitionStatusRequest, ChangeJobRequisitionStatusRequestValidator>(services);
        AddValidator<CreateJobRequisitionRequirementRequest, CreateJobRequisitionRequirementRequestValidator>(services);
        AddValidator<UpdateJobRequisitionRequirementRequest, UpdateJobRequisitionRequirementRequestValidator>(services);
        AddValidator<CreateCandidateEvaluationCaseRequest, CreateCandidateEvaluationCaseRequestValidator>(services);
        AddValidator<UpdateCandidateEvaluationCaseRequest, UpdateCandidateEvaluationCaseRequestValidator>(services);
        AddValidator<ChangeCandidateEvaluationCaseStatusRequest, ChangeCandidateEvaluationCaseStatusRequestValidator>(services);

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
