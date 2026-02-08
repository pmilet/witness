# Witness MCP Server - C# .NET 9.0 Implementation

A complete implementation of the Witness MCP Server using C# .NET 9.0 with Domain-Driven Design (DDD) and Clean Architecture principles.

## Architecture

The solution follows a layered architecture:

### 1. Domain Layer (`Witness.Domain`)
- **Entities**: `Interaction`, `Session`
- **Value Objects**: `WitnessId`, `HttpMethod`, `HttpRequest`, `HttpResponse`, `InteractionMetadata`
- **Services**: `IWitnessIdGenerator`, `WitnessIdGenerator`
- **Repositories**: `IInteractionRepository`, `ISessionRepository`

Contains the core business logic with no external dependencies.

### 2. Application Layer (`Witness.Application`)
- **Use Cases**: 
  - `RecordInteractionUseCase` - Execute and record HTTP interactions
  - `ReplayInteractionUseCase` - Replay recorded interactions
  - `InspectInteractionUseCase` - View interaction details
  - `ListSessionsUseCase` - List all sessions
  - `ListInteractionsUseCase` - List interactions in a session
- **DTOs**: Request/Response models for each use case
- **Interfaces**: `IHttpExecutor`

Orchestrates domain logic and defines application boundaries.

### 3. Infrastructure Layer (`Witness.Infrastructure`)
- **Repositories**: 
  - `FileSystemInteractionRepository` - JSON file-based interaction storage
  - `FileSystemSessionRepository` - JSON file-based session storage
- **HTTP**: `HttpExecutor` - HTTP request execution using HttpClient
- **Serialization**: Persistence models for JSON serialization

Implements infrastructure concerns and external dependencies.

### 4. MCP Server Layer (`Witness.McpServer`)
- **Handlers**: Tool handlers for each MCP tool
- **Models**: MCP protocol models
- **WitnessMcpServer**: JSON-RPC server implementation using StreamJsonRpc
- **Program.cs**: Application entry point with DI configuration

Implements the MCP protocol and tool interfaces.

## Features Implemented (Phase 1 MVP)

### ✅ witness/record
Execute an HTTP request and capture the full interaction.

**Input:**
```json
{
  "target": "https://api.example.com",
  "method": "POST",
  "path": "/api/users",
  "headers": {
    "Authorization": "Bearer token",
    "Content-Type": "application/json"
  },
  "body": "{\"name\":\"John\"}",
  "options": {
    "tag": "user-creation",
    "sessionId": "test-session-1",
    "followRedirects": true,
    "timeoutMs": 30000
  }
}
```

### ✅ witness/replay
Replay a recorded interaction against a different target.

**Input:**
```json
{
  "witnessId": "user-creation_POST_api-users_a3f7b2c1_20260208T1430",
  "target": "https://staging-api.example.com",
  "options": {
    "tag": "staging-replay",
    "overrideHeaders": {
      "Authorization": "Bearer new-token"
    }
  }
}
```

### ✅ witness/inspect
View details of a recorded interaction.

**Input:**
```json
{
  "witnessId": "user-creation_POST_api-users_a3f7b2c1_20260208T1430",
  "sessionId": "test-session-1"
}
```

### ✅ witness/list-sessions
List all recorded sessions.

### ✅ witness/list-interactions
List all interactions in a session.

**Input:**
```json
{
  "sessionId": "test-session-1"
}
```

## Storage Structure

Interactions are stored in the local filesystem:

```
witness-store/
  └── sessions/
      └── {sessionId}/
          ├── session.json
          └── interactions/
              ├── {witnessId}.json
              └── ...
```

## WitnessId Format

Unique identifier format: `{tag}_{method}_{path-slug}_{body-hash}_{timestamp}`

Example: `smoke_POST_api-loans_a3f7b2c1_20260208T1430`

- **tag**: User-provided label
- **method**: HTTP verb (GET, POST, etc.)
- **path-slug**: URL path with slashes→hyphens, max 60 chars
- **body-hash**: First 8 chars of SHA-256 hash of request body
- **timestamp**: Compact ISO format (yyyyMMddTHHmm)

## Building and Running

### Build
```bash
dotnet build
```

### Run
```bash
dotnet run --project src/Witness.McpServer/Witness.McpServer.csproj
```

The server communicates via stdio using the MCP protocol (JSON-RPC 2.0).

## Testing

Unit tests are located in `tests/Witness.Domain.Tests/`.

```bash
dotnet test
```

## Dependencies

- **.NET 9.0**
- **StreamJsonRpc** (2.24.84) - JSON-RPC communication
- **Microsoft.Extensions.Hosting** (10.0.2) - DI container and host
- **Microsoft.Extensions.Http** (10.0.2) - HttpClient factory
- **System.Text.Json** (10.0.2) - JSON serialization

## Clean Code Principles Applied

1. **SOLID Principles**
   - Single Responsibility: Each class has one reason to change
   - Open/Closed: Extensible without modification
   - Liskov Substitution: Proper abstraction hierarchies
   - Interface Segregation: Focused interfaces
   - Dependency Inversion: Depend on abstractions

2. **Domain-Driven Design**
   - Ubiquitous language throughout
   - Clear bounded contexts
   - Rich domain models with behavior
   - Aggregate roots and entities
   - Value objects for immutability

3. **Clean Architecture**
   - Domain layer has no dependencies
   - Application layer depends only on Domain
   - Infrastructure depends on Domain and Application
   - MCP Server depends on all layers
   - Dependency flow: outward to inward

4. **Code Quality**
   - Strong typing throughout
   - Immutability where appropriate (records, readonly collections)
   - Guard clauses for validation
   - Async/await for I/O operations
   - Proper exception handling
   - XML documentation on public APIs
   - No magic strings or numbers
   - Explicit naming conventions

## Future Enhancements (Phase 2+)

- **witness/compare** - Diff two recordings with tolerance settings
- **witness/chain** - Multi-step flow execution with variable extraction
- **witness/discover** - Parse OpenAPI specs
- **witness/generate** - AI-powered test scenario generation
- **witness/suite-run** - Batch execution with parallel support
- **witness/mock** - Mock server from recordings
- Azure Blob Storage adapter
- SQLite index for search
- Response comparison engine

## License

Apache-2.0
