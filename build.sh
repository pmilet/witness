#!/bin/bash
set -e

echo "====================================="
echo "  Building Witness .NET 9.0 Solution"
echo "====================================="

# Clean
echo "Cleaning..."
dotnet clean Witness.slnx --nologo -v q

# Restore
echo "Restoring packages..."
dotnet restore Witness.slnx --nologo -v q

# Build
echo "Building..."
dotnet build Witness.slnx --configuration Release --no-restore --nologo

# Test
echo "Running tests..."
dotnet test Witness.slnx --configuration Release --no-build --nologo --verbosity quiet

echo ""
echo "✅ Build successful! All tests passed."
echo ""
echo "To run the MCP server:"
echo "  dotnet run --project Witness.McpServer/Witness.McpServer.csproj"
