#!/bin/bash
set -e

echo "====================================="
echo "  Building Witness .NET 9.0 Solution"
echo "====================================="

# Clean
echo "Cleaning..."
dotnet clean src/Witness.slnx --nologo -v q

# Restore
echo "Restoring packages..."
dotnet restore src/Witness.slnx --nologo -v q

# Build
echo "Building..."
dotnet build src/Witness.slnx --configuration Release --no-restore --nologo

# Test
echo "Running tests..."
dotnet test src/Witness.slnx --configuration Release --no-build --nologo --verbosity quiet

echo ""
echo "✅ Build successful! All tests passed."
echo ""
echo "To run the MCP server:"
echo "  dotnet run --project src/Witness.McpServer/Witness.McpServer.csproj"
