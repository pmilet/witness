#!/bin/bash
set -e

echo "Setting up project references..."

# Application depends on Domain
dotnet add Witness.Application/Witness.Application.csproj reference Witness.Domain/Witness.Domain.csproj

# Infrastructure depends on Domain and Application
dotnet add Witness.Infrastructure/Witness.Infrastructure.csproj reference Witness.Domain/Witness.Domain.csproj
dotnet add Witness.Infrastructure/Witness.Infrastructure.csproj reference Witness.Application/Witness.Application.csproj

# McpServer depends on all layers
dotnet add Witness.McpServer/Witness.McpServer.csproj reference Witness.Domain/Witness.Domain.csproj
dotnet add Witness.McpServer/Witness.McpServer.csproj reference Witness.Application/Witness.Application.csproj
dotnet add Witness.McpServer/Witness.McpServer.csproj reference Witness.Infrastructure/Witness.Infrastructure.csproj

# Test project references
dotnet add Witness.Domain.Tests/Witness.Domain.Tests.csproj reference Witness.Domain/Witness.Domain.csproj
dotnet add Witness.Application.Tests/Witness.Application.Tests.csproj reference Witness.Application/Witness.Application.csproj
dotnet add Witness.Application.Tests/Witness.Application.Tests.csproj reference Witness.Domain/Witness.Domain.csproj
dotnet add Witness.Infrastructure.Tests/Witness.Infrastructure.Tests.csproj reference Witness.Infrastructure/Witness.Infrastructure.csproj
dotnet add Witness.Infrastructure.Tests/Witness.Infrastructure.Tests.csproj reference Witness.Domain/Witness.Domain.csproj

dotnet add Witness.Integration.Tests/Witness.Integration.Tests.csproj reference Witness.McpServer/Witness.McpServer.csproj
dotnet add Witness.Integration.Tests/Witness.Integration.Tests.csproj reference Witness.Infrastructure/Witness.Infrastructure.csproj

echo "Adding NuGet packages..."

# Application layer packages
dotnet add Witness.Application/Witness.Application.csproj package MediatR
dotnet add Witness.Application/Witness.Application.csproj package FluentValidation
dotnet add Witness.Application/Witness.Application.csproj package FluentValidation.DependencyInjectionExtensions
dotnet add Witness.Application/Witness.Application.csproj package Microsoft.Extensions.Logging.Abstractions

# Infrastructure packages
dotnet add Witness.Infrastructure/Witness.Infrastructure.csproj package Microsoft.Extensions.Options
dotnet add Witness.Infrastructure/Witness.Infrastructure.csproj package System.Text.Json
dotnet add Witness.Infrastructure/Witness.Infrastructure.csproj package Polly

# McpServer packages
dotnet add Witness.McpServer/Witness.McpServer.csproj package Microsoft.Extensions.Hosting
dotnet add Witness.McpServer/Witness.McpServer.csproj package Microsoft.Extensions.Configuration
dotnet add Witness.McpServer/Witness.McpServer.csproj package Microsoft.Extensions.Configuration.Json
dotnet add Witness.McpServer/Witness.McpServer.csproj package Microsoft.Extensions.DependencyInjection
dotnet add Witness.McpServer/Witness.McpServer.csproj package Microsoft.Extensions.Logging.Console

# Test packages
dotnet add Witness.Application.Tests/Witness.Application.Tests.csproj package Moq
dotnet add Witness.Application.Tests/Witness.Application.Tests.csproj package FluentAssertions
dotnet add Witness.Infrastructure.Tests/Witness.Infrastructure.Tests.csproj package Moq
dotnet add Witness.Infrastructure.Tests/Witness.Infrastructure.Tests.csproj package FluentAssertions
dotnet add Witness.Integration.Tests/Witness.Integration.Tests.csproj package FluentAssertions

echo "Dependencies setup complete!"
