using HrDecisionSupport.Application.Common.Interfaces;
using HrDecisionSupport.Application.EmployeeImports.Spreadsheet;
using HrDecisionSupport.Infrastructure.EmployeeImports;
using HrDecisionSupport.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HrDecisionSupport.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        services.AddDbContext<HrDecisionSupportDbContext>(configureDbContext);
        services.AddScoped<IHrDecisionSupportDbContext>(serviceProvider =>
            serviceProvider.GetRequiredService<HrDecisionSupportDbContext>());
        services.AddSingleton<IEmployeeSpreadsheetReader, ClosedXmlEmployeeSpreadsheetReader>();
        services.AddScoped<IEmployeeImportTransactionRunner, EmployeeImportTransactionRunner>();

        return services;
    }
}
