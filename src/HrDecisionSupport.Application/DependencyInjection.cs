using Microsoft.Extensions.DependencyInjection;

namespace HrDecisionSupport.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Concrete application services will be registered in Milestone 5.2.
        return services;
    }
}
