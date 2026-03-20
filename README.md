# Witness MCP Server

An MCP server that gives AI agents the power to record, replay, and compare HTTP API interactions.

- **[Documentation](docs/README.md)** — full docs, quickstart, architecture, spec
- **[Source](src/)** — .NET 9.0 solution (`src/Witness.slnx`)
- **[Demo](demo/)** — simulation test with Docker-based legacy/modern APIs

## Quick start

```bash
./build.sh
dotnet run --project src/Witness.McpServer/Witness.McpServer.csproj
```
