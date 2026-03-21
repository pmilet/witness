using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Witness.Domain.Repositories;
using Witness.Infrastructure.Repositories;

namespace Witness.Infrastructure.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddInfrastructure_WithLocalStorageType_RegistersFileSystemRepositories()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Witness:Storage:Type"] = "local",
            ["Witness:Storage:Path"] = Path.GetTempPath()
        });

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        var interactionRepo = provider.GetRequiredService<IInteractionRepository>();
        var sessionRepo = provider.GetRequiredService<ISessionRepository>();

        Assert.IsType<FileSystemInteractionRepository>(interactionRepo);
        Assert.IsType<FileSystemSessionRepository>(sessionRepo);
    }

    [Fact]
    public void AddInfrastructure_WithDefaultStorageType_RegistersFileSystemRepositories()
    {
        // Arrange - no storage type explicitly set (defaults to "local")
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Witness:Storage:Path"] = Path.GetTempPath()
        });

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.IsType<FileSystemInteractionRepository>(provider.GetRequiredService<IInteractionRepository>());
        Assert.IsType<FileSystemSessionRepository>(provider.GetRequiredService<ISessionRepository>());
    }

    [Fact]
    public void AddInfrastructure_WithAzureStorageType_RegistersAzureBlobRepositories()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Witness:Storage:Type"] = "azure",
            ["Witness:Storage:ConnectionString"] = "UseDevelopmentStorage=true",
            ["Witness:Storage:ContainerName"] = "test-container"
        });

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.IsType<AzureBlobInteractionRepository>(provider.GetRequiredService<IInteractionRepository>());
        Assert.IsType<AzureBlobSessionRepository>(provider.GetRequiredService<ISessionRepository>());
    }

    [Fact]
    public void AddInfrastructure_WithAzureStorageTypeButNoConnectionString_ThrowsOnResolution()
    {
        // Arrange
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Witness:Storage:Type"] = "azure"
            // No ConnectionString
        });

        var services = new ServiceCollection();
        services.AddLogging();

        // Act & Assert - exception should be thrown when registering (not lazily on resolve)
        Assert.Throws<InvalidOperationException>(() => services.AddInfrastructure(configuration));
    }

    [Fact]
    public void AddInfrastructure_StorageTypeCaseInsensitive_RegistersAzureBlobRepositories()
    {
        // Arrange - using mixed case "Azure"
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["Witness:Storage:Type"] = "Azure",
            ["Witness:Storage:ConnectionString"] = "UseDevelopmentStorage=true"
        });

        var services = new ServiceCollection();
        services.AddLogging();

        // Act
        services.AddInfrastructure(configuration);
        var provider = services.BuildServiceProvider();

        // Assert
        Assert.IsType<AzureBlobInteractionRepository>(provider.GetRequiredService<IInteractionRepository>());
        Assert.IsType<AzureBlobSessionRepository>(provider.GetRequiredService<ISessionRepository>());
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
