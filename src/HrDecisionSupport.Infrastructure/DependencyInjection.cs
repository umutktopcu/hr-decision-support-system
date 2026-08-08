using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Application.CandidateImports.Spreadsheet;
using HrDecisionSupport.Application.SemanticMatching.Embeddings;
using HrDecisionSupport.Application.SemanticMatching.Retrieval;
using HrDecisionSupport.Infrastructure.EmployeeImports;
using HrDecisionSupport.Infrastructure.CandidateImports;
using HrDecisionSupport.Infrastructure.Persistence;
using HrDecisionSupport.Infrastructure.SemanticMatching;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HrDecisionSupport.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext,
        IConfiguration? configuration = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        services.AddDbContext<HrDecisionSupportDbContext>(configureDbContext);
        services.AddScoped<IHrDecisionSupportDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<HrDecisionSupportDbContext>());
        services.AddSingleton<IEmployeeSpreadsheetReader, ClosedXmlEmployeeSpreadsheetReader>();
        services.AddSingleton<ICandidateImportSourceReader, ClosedXmlCandidateSpreadsheetReader>();
        services.AddScoped<IEmployeeImportTransactionRunner, EmployeeImportTransactionRunner>();

        // Semantic matching
        services.AddScoped<ISemanticRetrievalService, SemanticRetrievalService>();

        if (configuration is not null)
        {
            services.Configure<QwenEmbeddingOptions>(
                configuration.GetSection(QwenEmbeddingOptions.SectionName));
        }
        else
        {
            services.Configure<QwenEmbeddingOptions>(_ => { }); // defaults
        }

        services.AddHttpClient<IEmbeddingProvider, QwenEmbeddingProvider>((serviceProvider, client) =>
        {
            var opts = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<QwenEmbeddingOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        if (configuration is not null)
        {
            services.Configure<HrDecisionSupport.Infrastructure.SemanticMatching.Reranking.QwenRerankerOptions>(
                configuration.GetSection(HrDecisionSupport.Infrastructure.SemanticMatching.Reranking.QwenRerankerOptions.SectionName));
        }
        else
        {
            services.Configure<HrDecisionSupport.Infrastructure.SemanticMatching.Reranking.QwenRerankerOptions>(_ => { });
        }

        services.AddHttpClient<HrDecisionSupport.Application.SemanticMatching.Reranking.ICrossEncoderProvider, HrDecisionSupport.Infrastructure.SemanticMatching.Reranking.QwenCrossEncoderProvider>((serviceProvider, client) =>
        {
            var opts = serviceProvider
                .GetRequiredService<Microsoft.Extensions.Options.IOptions<HrDecisionSupport.Infrastructure.SemanticMatching.Reranking.QwenRerankerOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        });

        return services;
    }
}
