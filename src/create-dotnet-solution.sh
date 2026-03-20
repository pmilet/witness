#!/bin/bash
set -e

# Create solution
dotnet new sln -n Witness

# Create Domain layer (class library)
dotnet new classlib -n Witness.Domain -f net9.0
dotnet sln add Witness.Domain/Witness.Domain.csproj

# Create Application layer (class library)
dotnet new classlib -n Witness.Application -f net9.0
dotnet sln add Witness.Application/Witness.Application.csproj

# Create Infrastructure layer (class library)
dotnet new classlib -n Witness.Infrastructure -f net9.0
dotnet sln add Witness.Infrastructure/Witness.Infrastructure.csproj

# Create MCP Server (console app)
dotnet new console -n Witness.McpServer -f net9.0
dotnet sln add Witness.McpServer/Witness.McpServer.csproj

# Create Unit Tests
dotnet new xunit -n Witness.Domain.Tests -f net9.0
dotnet sln add Witness.Domain.Tests/Witness.Domain.Tests.csproj

dotnet new xunit -n Witness.Application.Tests -f net9.0
dotnet sln add Witness.Application.Tests/Witness.Application.Tests.csproj

dotnet new xunit -n Witness.Infrastructure.Tests -f net9.0
dotnet sln add Witness.Infrastructure.Tests/Witness.Infrastructure.Tests.csproj

# Create Integration Tests
dotnet new xunit -n Witness.Integration.Tests -f net9.0
dotnet sln add Witness.Integration.Tests/Witness.Integration.Tests.csproj

echo "Solution structure created successfully!"
