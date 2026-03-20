# Witness

> ⚠️ **Alpha** — under active development. Breaking changes may occur.

**An MCP server that gives AI agents the ability to record, replay, and compare HTTP API interactions.**

Think of it as a flight recorder for your REST APIs — controlled by your AI agent, not by you. Every request and response is captured as a structured, replayable artifact that can be diffed against any other recording.

---

## Why it exists

Testing API migrations, version upgrades, and backend replacements is tedious and error-prone when done manually. Witness automates the evidence-gathering:

- Record the behavior of the **old** system.
- Replay the same requests against the **new** system.
- Compare the responses — field by field.

No test scripts to write. No assertions to maintain. The AI agent drives the whole workflow through tool calls.

---

## Quick Start

```bash
git clone https://github.com/pmilet/witness.git
cd witness
dotnet build src/Witness.slnx
```

Configure your MCP client:

**Claude Desktop / Claude Code:**
```json
{
  "mcpServers": {
    "witness": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/witness/src/Witness.McpServer/Witness.McpServer.csproj", "--no-build"]
    }
  }
}
```

**VS Code (GitHub Copilot):**
```json
{
  "servers": {
    "witness": {
      "command": "dotnet",
      "args": ["run", "--project", "/path/to/witness/src/Witness.McpServer/Witness.McpServer.csproj", "--no-build"]
    }
  }
}
```

See [QUICKSTART.md](QUICKSTART.md) for detailed setup instructions.

---

## MCP Tools

| Tool | Purpose |
|------|---------|
| `witness/record` | Execute an HTTP request and capture the full interaction |
| `witness/replay` | Replay a recorded interaction against a different target |
| `witness/compare` | Diff two recorded interactions field by field |
| `witness/list` | Browse sessions and their interactions |
| `witness/inspect` | View full details of a single interaction |

---

## Outbound Record/Replay

The `Witness.AspNetCore` library enables capturing and replaying **outbound HTTP calls** your API makes during request processing.

```
RECORD:  POST /api/orders → API → GET external-api.com/data → real response (stored)
REPLAY:  POST /api/orders → API → GET external-api.com/data → recorded response (no network)
```

### Integration

```csharp
// Capture outbound calls
builder.Services.AddHttpClient("external-api")
    .AddWitnessCapture(opt => opt.SessionId = "my-session");

// Enable record/replay middleware
app.UseWitnessMiddleware(opt => opt.StorePath = "./witness-store");
```

### Protocol

| Header | Description |
|--------|-------------|
| `X-Witness-Mode: record` | Capture outbound calls with the inbound interaction |
| `X-Witness-Mode: replay` | Stub outbound calls with previously recorded responses |
| `X-Witness-Id: {id}` | The WitnessId to replay (required for replay mode) |

See [QUICKSTART.md](QUICKSTART.md) for integration details.

---

## Use Cases

- **API migration** — Record legacy behavior, replay against the new service, compare.
- **Version upgrade** — Record against v1, replay against v2. Prove compatibility.
- **Regression testing** — Record a baseline, replay after deploy. Any change shows up.
- **Offline development** — Record third-party API interactions, replay from recordings.
- **Outbound stubbing** — Record with outbound calls, replay with stubs. Fully deterministic.

---

## Architecture

The solution follows Domain-Driven Design with Clean Architecture:

```
Witness.McpServer        → MCP protocol host (JSON-RPC 2.0 over STDIO)
Witness.Application      → CQRS commands and queries (MediatR)
Witness.Domain           → Aggregates, value objects, repository interfaces
Witness.Infrastructure   → Storage, HTTP execution (Polly retry)
Witness.AspNetCore       → Outbound capture handler, record/replay middleware
```

See [README-DOTNET.md](README-DOTNET.md) for detailed architecture documentation.

---

## Demo API

A sample ASP.NET API demonstrating both inbound recording and outbound capture:

```bash
# Run locally
dotnet run --project demo/Witness.DemoApi/Witness.DemoApi.csproj

# Run with Docker
cd demo && docker compose up demo-api -d
# Available at http://localhost:5080
```

---

## Documentation

- [Quick Start Guide](QUICKSTART.md) — Installation, configuration, first steps
- [.NET Architecture](README-DOTNET.md) — DDD architecture, project structure
- [Usage Examples](examples/USAGE.md) — Ready-to-use scenarios
- [Development Guide](DEVELOPMENT.md) — Contributing and development workflow
- [Full Specification](witness-mcp-server-spec.md) — Complete tool definitions and data model

---

## License

[Apache-2.0](../LICENSE)

---

<p align="center"><i>Every HTTP interaction is evidence. Witness captures it.</i></p>
