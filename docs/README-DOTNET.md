# Witness - .NET 9.0 Implementation

> **⚠️ ALPHA VERSION WARNING**  
> This software is currently in **alpha stage** and under active development.  
> Features may change, and breaking changes may occur without notice.  
> Use in production environments at your own risk.

**An MCP server that gives AI agents the power to record, replay, and compare HTTP API interactions.**

This is the .NET 9.0 implementation of the Witness MCP Server, rebuilt from the ground up with Domain-Driven Design (DDD), Clean Code principles, and CQRS pattern.

---

## Architecture Overview

The solution follows a clean, layered DDD architecture:

```
┌──────────────────────────────────────────────────────────┐
│                 Witness.McpServer (Host)                  │
│                 - MCP Protocol Handler                    │
│                 - JSON-RPC 2.0 over STDIO                 │
└───────────────────┬──────────────────────────────────────┘
                    │
┌───────────────────▼──────────────────────────────────────┐
│           Witness.Application (Use Cases)                 │
│    - Commands: RecordInteraction, ReplayInteraction       │
│    - Queries: InspectInteraction, ListSessions/Interactions │
│    - MediatR for CQRS                                     │
│    - FluentValidation for input validation               │
└───────────────────┬──────────────────────────────────────┘
                    │
┌───────────────────▼──────────────────────────────────────┐
│              Witness.Domain (Core Logic)                  │
│    - Aggregates: Interaction, Session                     │
│    - Value Objects: WitnessId, HttpRequest, HttpResponse  │
│    - Repository Interfaces                                │
│    - Domain Services                                      │
└───────────────────┬──────────────────────────────────────┘
                    │
┌───────────────────▼──────────────────────────────────────┐
│       Witness.Infrastructure (External Concerns)          │
│    - FileSystemInteractionRepository                      │
│    - FileSystemSessionRepository                          │
│    - HttpExecutorService (with Polly retry policies)      │
└───────────────────────────────────────────────────────────┘
```

### Key Design Patterns

- **Domain-Driven Design (DDD)**: Clear separation of domain logic, entities, value objects, and aggregates
- **CQRS**: Commands for mutations, Queries for reads using MediatR
- **Repository Pattern**: Abstraction over data persistence
- **Options Pattern**: Type-safe configuration
- **Dependency Injection**: Constructor injection throughout
- **Clean Architecture**: Dependencies point inward

---

## Quick Start

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)

### Build

```bash
# Clone the repository
git clone https://github.com/pmilet/witness.git
cd witness

# Build the solution
dotnet build Witness.slnx

# Run tests
dotnet test Witness.slnx
```

### Run

```bash
# Run the MCP server
dotnet run --project Witness.McpServer/Witness.McpServer.csproj
```

### Configuration

Edit `Witness.McpServer/appsettings.json`:

```json
{
  "Witness": {
    "Storage": {
      "Type": "local",
      "Path": "./witness-store"
    },
    "Defaults": {
      "TimeoutMs": 30000,
      "FollowRedirects": true
    }
  }
}
```

### Add to MCP Client

**Claude Desktop / Claude Code:**
```json
{
  "mcpServers": {
    "witness": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/witness/Witness.McpServer/Witness.McpServer.csproj"]
    }
  }
}
```

**VS Code (GitHub Copilot):**
```json
{
  "mcp": {
    "servers": {
      "witness": {
        "command": "dotnet",
        "args": ["run", "--project", "/absolute/path/to/witness/Witness.McpServer/Witness.McpServer.csproj"]
      }
    }
  }
}
```

---

## Project Structure

```
Witness/
├── Witness.Domain/                  # Core domain logic
│   ├── Entities/                    # Aggregate roots and entities
│   │   ├── Interaction.cs           # Main aggregate
│   │   ├── Session.cs               # Session entity
│   │   └── InteractionMetadata.cs   # Metadata entity
│   ├── ValueObjects/                # Immutable value objects
│   │   ├── WitnessId.cs             # Deterministic ID generator
│   │   ├── HttpRequest.cs           # HTTP request data
│   │   └── HttpResponse.cs          # HTTP response data
│   ├── Repositories/                # Repository interfaces
│   │   ├── IInteractionRepository.cs
│   │   └── ISessionRepository.cs
│   └── Services/                    # Domain services
│       └── IHttpExecutor.cs
├── Witness.Application/             # Application use cases
│   ├── Commands/                    # Write operations (CQRS)
│   │   ├── RecordInteractionCommand.cs
│   │   ├── RecordInteractionHandler.cs
│   │   ├── ReplayInteractionCommand.cs
│   │   └── ReplayInteractionHandler.cs
│   ├── Queries/                     # Read operations (CQRS)
│   │   ├── InspectInteractionQuery.cs
│   │   ├── ListSessionsQuery.cs
│   │   └── ListInteractionsQuery.cs
│   ├── DTOs/                        # Data transfer objects
│   │   ├── InteractionDto.cs
│   │   └── SessionDto.cs
│   └── Validators/                  # FluentValidation validators
│       └── RecordInteractionValidator.cs
├── Witness.Infrastructure/          # External concerns
│   ├── Services/                    # Infrastructure services
│   │   └── HttpExecutorService.cs   # HTTP client with Polly
│   ├── Repositories/                # Repository implementations
│   │   ├── FileSystemInteractionRepository.cs
│   │   └── FileSystemSessionRepository.cs
│   ├── Persistence/                 # Persistence models
│   │   ├── InteractionModel.cs
│   │   └── SessionModel.cs
│   └── Configuration/               # Configuration classes
│       └── WitnessOptions.cs
├── Witness.McpServer/               # MCP server host
│   ├── Program.cs                   # Main entry point
│   ├── McpTools/                    # MCP tool definitions
│   │   └── McpToolDefinitions.cs
│   └── appsettings.json             # Configuration
├── Witness.Domain.Tests/            # Domain unit tests
├── Witness.Application.Tests/       # Application unit tests
├── Witness.Infrastructure.Tests/    # Infrastructure unit tests
└── Witness.Integration.Tests/       # End-to-end tests
```

---

## MCP Tools

The server exposes 4 MCP tools:

### 1. `witness/record`
Execute an HTTP request and capture the full interaction.

**Parameters:**
- `target` (string, required): Base URL of the target API
- `method` (string, required): HTTP method (GET, POST, PUT, DELETE, PATCH)
- `path` (string, required): Request path
- `headers` (object, optional): HTTP headers
- `body` (object|string, optional): Request body
- `options` (object, optional):
  - `tag` (string): Tag for this interaction
  - `sessionId` (string): Session ID to group interactions
  - `description` (string): Human-readable description
  - `timeoutMs` (number): Request timeout in ms
  - `followRedirects` (boolean): Whether to follow redirects

**Returns:**
- `witnessId`: Generated WitnessId
- `sessionId`: Session ID
- `statusCode`: HTTP status code
- `durationMs`: Request duration
- `responseBody`: Response body
- `responseHeaders`: Response headers
- `stored`: Whether interaction was stored

### 2. `witness/replay`
Replay a recorded interaction against a different target.

**Parameters:**
- `witnessId` (string, required): WitnessId to replay
- `target` (string, required): New target URL
- `options` (object, optional):
  - `tag` (string): Tag for the replay
  - `sessionId` (string): Session ID for the replay
  - `overrideHeaders` (object): Headers to override

**Returns:**
- `originalWitnessId`: Original WitnessId
- `replayWitnessId`: New WitnessId for the replay
- `statusCode`: HTTP status code
- `durationMs`: Request duration
- `responseBody`: Response body
- `stored`: Whether replay was stored

### 3. `witness/inspect`
View full details of a recorded interaction.

**Parameters:**
- `witnessId` (string, required): WitnessId to inspect
- `sessionId` (string, optional): Session ID to narrow search

**Returns:**
- Full `InteractionDto` with request, response, and metadata

### 4. `witness/list`
List sessions or interactions.

**Parameters:**
- `sessionId` (string, optional): If provided, lists interactions in that session. If omitted, lists all sessions.
- `limit` (number, optional): Maximum results to return (default: 50)

**Returns:**
- When listing sessions: `{ count, total, sessions: [...] }`
- When listing interactions: `{ sessionId, count, total, interactions: [...] }`

---

## Testing

The solution includes comprehensive unit tests:

```bash
# Run all tests
dotnet test Witness.slnx

# Run specific test project
dotnet test Witness.Domain.Tests/
dotnet test Witness.Application.Tests/
dotnet test Witness.Infrastructure.Tests/

# Run with coverage
dotnet test /p:CollectCoverage=true
```

### Test Coverage

- **Domain Tests (22 tests)**: WitnessId generation, entity validation, domain behavior
- **Application Tests (2 tests)**: Command/query handlers with mocked dependencies
- **Infrastructure Tests**: Repository and HTTP executor tests (TODO)
- **Integration Tests**: End-to-end MCP protocol tests (TODO)

---

## Storage Format

Interactions are stored as JSON files in the following structure:

```
witness-store/
└── sessions/
    └── {sessionId}/
        ├── session.json          # Session metadata
        └── interactions/
            └── {witnessId}.json  # Interaction data
```

**Backward Compatibility**: The .NET implementation uses the same JSON storage format as the TypeScript version, ensuring seamless migration and interoperability.

---

## Development

### Adding a New Command

1. Create command and result classes in `Witness.Application/Commands/`
2. Create handler implementing `IRequestHandler<TCommand, TResult>`
3. Add validator in `Witness.Application/Validators/`
4. Write unit tests with mocked dependencies
5. Register in MCP server's `HandleToolCallAsync`

### Adding a New Query

1. Create query and result classes in `Witness.Application/Queries/`
2. Create handler implementing `IRequestHandler<TQuery, TResult>`
3. Write unit tests
4. Register in MCP server's `HandleToolCallAsync`

### Extending Storage

To add a new storage provider (e.g., Azure Blob Storage):

1. Create new repository implementations in `Witness.Infrastructure/Repositories/`
2. Implement `IInteractionRepository` and `ISessionRepository`
3. Register in `Witness.Infrastructure/DependencyInjection.cs` based on configuration
4. Update `WitnessOptions` configuration class

---

## Clean Code & Best Practices

This implementation follows:

✅ **SOLID Principles**
- Single Responsibility: Each class has one clear purpose
- Open/Closed: Extensible through interfaces
- Liskov Substitution: Proper inheritance hierarchies
- Interface Segregation: Focused interfaces (IInteractionRepository, ISessionRepository)
- Dependency Inversion: All dependencies are on abstractions

✅ **Domain-Driven Design**
- Ubiquitous language throughout the codebase
- Bounded contexts (domain, application, infrastructure)
- Aggregates with clear boundaries (Interaction)
- Value objects for immutable data (WitnessId, HttpRequest)
- Repository pattern for persistence abstraction

✅ **Clean Code**
- Meaningful names
- Small, focused methods
- No magic numbers
- Comprehensive XML documentation
- Proper use of C# records and pattern matching

✅ **Testing**
- Unit tests for all domain logic
- Integration tests for external concerns
- Mocking for isolated testing
- Arrange-Act-Assert pattern

---

## Migration from TypeScript

The .NET implementation is a complete rewrite maintaining functional parity with the TypeScript version:

| Feature | TypeScript | .NET 9.0 |
|---------|-----------|----------|
| WitnessId Generation | ✅ | ✅ |
| HTTP Recording | ✅ | ✅ |
| Replay | ✅ | ✅ |
| Inspect | ✅ | ✅ |
| List Sessions/Interactions | ✅ | ✅ |
| File System Storage | ✅ | ✅ |
| MCP Protocol Support | ✅ | ✅ |
| Storage Format Compatibility | ✅ | ✅ |
| Unit Tests | ✅ | ✅ |
| Compare (Diff Engine) | ❌ | ❌ (Future) |
| OpenAPI Discovery | ❌ | ❌ (Future) |
| Mock Server | ❌ | ❌ (Future) |
| Azure Blob Storage | ❌ | ❌ (Future) |

---

## Roadmap

- [ ] Integration tests for full MCP protocol flow
- [ ] Compare/Diff engine with tolerance configuration
- [ ] OpenAPI discovery and validation
- [ ] Mock server from recorded interactions
- [ ] Azure Blob Storage provider
- [ ] Performance benchmarks
- [ ] Docker container image
- [ ] NuGet package publication

---

## Contributing

Contributions are welcome! Please ensure:

1. All tests pass: `dotnet test`
2. Code follows existing patterns and conventions
3. New features include unit tests
4. XML documentation for public APIs

---

## License

[Apache-2.0](LICENSE)

---

<p align="center">
  <i>Every HTTP interaction is evidence. Witness captures it. Now in .NET 9.0.</i>
</p>
