# Witness MCP Server - Development Guide

## Project Structure

```
witness/
├── src/
│   ├── Witness.Domain/              # Core domain logic
│   │   ├── Entities/                # Interaction, Session, InteractionMetadata
│   │   ├── ValueObjects/            # WitnessId, HttpRequest, HttpResponse
│   │   ├── Repositories/            # IInteractionRepository, ISessionRepository
│   │   └── Services/                # IHttpExecutor
│   ├── Witness.Application/         # CQRS use cases
│   │   ├── Commands/                # RecordInteraction, ReplayInteraction
│   │   ├── Queries/                 # InspectInteraction, ListSessions
│   │   ├── DTOs/                    # Data transfer objects
│   │   └── Validators/              # FluentValidation validators
│   ├── Witness.Infrastructure/      # External concerns
│   │   ├── Services/                # HttpExecutorService (with Polly)
│   │   ├── Repositories/            # FileSystem repositories
│   │   ├── Persistence/             # InteractionModel, SessionModel
│   │   └── Configuration/           # WitnessOptions
│   ├── Witness.AspNetCore/          # ASP.NET Core integration
│   │   ├── WitnessCaptureHandler.cs # Outbound HTTP capture
│   │   ├── WitnessCallContext.cs    # AsyncLocal correlation
│   │   ├── WitnessMiddleware.cs     # Record/replay middleware
│   │   └── ServiceCollectionExtensions.cs
│   ├── Witness.McpServer/           # MCP server host
│   ├── Witness.Domain.Tests/        # Domain unit tests
│   ├── Witness.Application.Tests/   # Application unit tests
│   ├── Witness.Infrastructure.Tests/ # Infrastructure tests
│   └── Witness.Integration.Tests/   # End-to-end tests
├── demo/
│   ├── Witness.DemoApi/             # Sample API for testing
│   ├── docker-compose.yml           # Docker setup
│   └── docker/                      # Legacy/modern Node.js demo APIs
└── docs/                            # Documentation
```

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Docker (optional, for demo)

## Development Setup

```bash
git clone https://github.com/pmilet/witness.git
cd witness
dotnet build src/Witness.slnx
dotnet test src/Witness.slnx
```

## Running the MCP Server

```bash
dotnet run --project src/Witness.McpServer/Witness.McpServer.csproj
```

## Running the Demo API

```bash
# Locally
dotnet run --project demo/Witness.DemoApi/Witness.DemoApi.csproj

# With Docker
cd demo && docker compose up demo-api -d
```

## Architecture

### Core Components

1. **Witness.Domain** — Aggregate roots (`Interaction`, `Session`), value objects (`WitnessId`, `HttpRequest`, `HttpResponse`), repository interfaces, and domain services.

2. **Witness.Application** — CQRS command/query handlers using MediatR. Commands: `RecordInteraction`, `ReplayInteraction`. Queries: `InspectInteraction`, `ListSessions`, `ListInteractions`.

3. **Witness.Infrastructure** — `HttpExecutorService` (HTTP client with Polly retry), `FileSystemInteractionRepository`, `FileSystemSessionRepository`, configuration via Options pattern.

4. **Witness.AspNetCore** — ASP.NET Core integration library:
   - `WitnessCaptureHandler` — `DelegatingHandler` that captures outbound `HttpClient` calls
   - `WitnessMiddleware` — Middleware for record/replay with outbound call correlation
   - `WitnessCallContext` — `AsyncLocal`-based correlation context (Record/Replay modes)
   - Extension methods: `AddWitnessCapture()`, `UseWitnessMiddleware()`

5. **Witness.McpServer** — MCP protocol host exposing tools via JSON-RPC 2.0 over STDIO.

### Key Design Patterns

- **Domain-Driven Design**: Bounded contexts, aggregates, value objects
- **CQRS**: Commands for writes, queries for reads via MediatR
- **Repository Pattern**: Abstraction over storage
- **AsyncLocal Correlation**: Links outbound calls to parent inbound requests
- **DelegatingHandler**: Transparent outbound HTTP interception

### Data Model

```csharp
Interaction
├── Id: WitnessId                    // Deterministic identifier
├── SessionId: string                // Session grouping
├── Timestamp: DateTime
├── Request: HttpRequest             // Method, URL, path, headers, body
├── Response: HttpResponse           // Status, headers, body, duration
├── Metadata: InteractionMetadata    // Tags, description, chain info
└── OutboundCalls: List<Interaction>? // Captured outbound calls (record/replay)
```

## Testing

```bash
# Run all tests
dotnet test src/Witness.slnx

# Run specific test project
dotnet test src/Witness.Domain.Tests/
dotnet test src/Witness.Application.Tests/
dotnet test src/Witness.Infrastructure.Tests/

# Run with verbose output
dotnet test src/Witness.slnx --verbosity normal
```

## Adding a New Command

1. Create command and result records in `Witness.Application/Commands/`
2. Create handler implementing `IRequestHandler<TCommand, TResult>`
3. Add validator in `Witness.Application/Validators/`
4. Register the MCP tool in `Witness.McpServer/McpTools/McpToolDefinitions.cs`
5. Write unit tests with mocked dependencies

## Adding a New Query

1. Create query and result records in `Witness.Application/Queries/`
2. Create handler implementing `IRequestHandler<TQuery, TResult>`
3. Register in MCP tool definitions
4. Write unit tests

## Extending Storage

To add a new storage provider (e.g., Azure Blob Storage):

1. Implement `IInteractionRepository` and `ISessionRepository`
2. Register in DI based on configuration
3. Update `WitnessOptions` configuration class

## Test Against Real APIs

Use free test APIs for development:
- JSONPlaceholder: https://jsonplaceholder.typicode.com
- ReqRes: https://reqres.in/api
- httpbin: https://httpbin.org

## Docker

### Build the demo API image

```bash
cd demo && docker compose build demo-api
```

### Run all demo services

```bash
cd demo && docker compose up -d
```

This starts:
- `demo-api` (port 5080) — .NET demo API with outbound capture
- `legacy-api` (port 3001) — Node.js legacy API
- `modern-api` (port 3002) — Node.js modern API

## License

Apache-2.0 — See LICENSE file for details.
