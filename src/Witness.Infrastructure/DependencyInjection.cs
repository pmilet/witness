using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Witness.Domain.Repositories;
using Witness.Domain.Services;
using Witness.Infrastructure.Configuration;
using Witness.Infrastructure.Repositories;
using Witness.Infrastructure.Services;

namespace Witness.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<WitnessOptions>(configuration.GetSection(WitnessOptions.SectionName));

        // HTTP Client
        services.AddHttpClient<IHttpExecutor, HttpExecutorService>()
            .SetHandlerLifetime(TimeSpan.FromMinutes(5));

        // Repositories
        services.AddSingleton<IInteractionRepository, FileSystemInteractionRepository>();
        services.AddSingleton<ISessionRepository, FileSystemSessionRepository>();

        return services;
    }
}
