using Azure.Storage.Blobs;
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

        // Repositories - choose implementation based on storage type
        var storageType = configuration[$"{WitnessOptions.SectionName}:{nameof(WitnessOptions.Storage)}:{nameof(StorageOptions.Type)}"]
            ?? "local";

        if (storageType.Equals("azure", StringComparison.OrdinalIgnoreCase))
        {
            var connectionString = configuration[$"{WitnessOptions.SectionName}:{nameof(WitnessOptions.Storage)}:{nameof(StorageOptions.ConnectionString)}"]
                ?? throw new InvalidOperationException(
                    "Azure Blob Storage connection string is required when storage type is 'azure'. " +
                    $"Set '{WitnessOptions.SectionName}:{nameof(WitnessOptions.Storage)}:{nameof(StorageOptions.ConnectionString)}' in configuration.");

            services.AddSingleton(_ => new BlobServiceClient(connectionString));
            services.AddSingleton<IInteractionRepository, AzureBlobInteractionRepository>();
            services.AddSingleton<ISessionRepository, AzureBlobSessionRepository>();
        }
        else
        {
            services.AddSingleton<IInteractionRepository, FileSystemInteractionRepository>();
            services.AddSingleton<ISessionRepository, FileSystemSessionRepository>();
        }

        return services;
    }
}
