using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Witness.Application;
using Witness.Infrastructure;
using Xunit;

namespace Witness.Integration.Tests;

public class McpServerIntegrationTests
{
    [Fact]
    public void ServiceProvider_WithAllLayers_CanBeBuilt()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Witness:Storage:Type"] = "local",
                ["Witness:Storage:Path"] = "./test-witness-store",
                ["Witness:Defaults:TimeoutMs"] = "30000",
                ["Witness:Defaults:FollowRedirects"] = "true"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        // Act
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        Assert.NotNull(serviceProvider);
        
        // Verify key services are registered
        var mediator = serviceProvider.GetService<MediatR.IMediator>();
        Assert.NotNull(mediator);
    }

    [Fact]
    public void DependencyInjection_ResolvesDomainServices()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Witness:Storage:Path"] = "./test-witness-store"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        var serviceProvider = services.BuildServiceProvider();

        // Act
        var httpExecutor = serviceProvider.GetService<Witness.Domain.Services.IHttpExecutor>();
        var interactionRepo = serviceProvider.GetService<Witness.Domain.Repositories.IInteractionRepository>();
        var sessionRepo = serviceProvider.GetService<Witness.Domain.Repositories.ISessionRepository>();

        // Assert
        Assert.NotNull(httpExecutor);
        Assert.NotNull(interactionRepo);
        Assert.NotNull(sessionRepo);
    }
}
