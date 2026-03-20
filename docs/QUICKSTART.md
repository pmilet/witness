# Witness MCP Server - Quick Start Guide

> ⚠️ Alpha — under active development. Breaking changes may occur.

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Docker (optional, for the demo API)

## Build from source

```bash
git clone https://github.com/pmilet/witness.git
cd witness
dotnet build src/Witness.slnx
```

## Configure your MCP client

### Claude Desktop / Claude Code

Add to your MCP config:
```json
{
  "mcpServers": {
    "witness": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/witness/src/Witness.McpServer/Witness.McpServer.csproj", "--no-build"]
    }
  }
}
```

### VS Code (GitHub Copilot)

Add to `.vscode/mcp.json`:
```json
{
  "servers": {
    "witness": {
      "command": "dotnet",
      "args": ["run", "--project", "/absolute/path/to/witness/src/Witness.McpServer/Witness.McpServer.csproj", "--no-build"]
    }
  }
}
```

## First steps

### 1. Record an interaction

Ask your AI agent:
> "Use witness/record to call GET https://jsonplaceholder.typicode.com/posts/1"

This will:
- Execute the HTTP request
- Capture the full request and response
- Store the interaction as a JSON file
- Return a `WitnessId` like `interaction_GET_posts-1_00000000_20260320T1437`

### 2. List recorded sessions

> "Use witness/list to show all recorded sessions"

### 3. Inspect an interaction

> "Use witness/inspect to view the details of WitnessId: {the-id-from-step-1}"

### 4. Replay against a different target

> "Use witness/replay to replay that request against https://other-api.example.com"

## Using the Demo API

The demo API demonstrates both inbound recording and outbound call capture.

### Run locally

```bash
dotnet run --project demo/Witness.DemoApi/Witness.DemoApi.csproj
# Runs on http://localhost:5000
```

### Run with Docker

```bash
cd demo
docker compose up demo-api -d
# Runs on http://localhost:5080
```

### Demo API endpoints

| Endpoint | Type | Description |
|----------|------|-------------|
| `GET /api/products` | Inbound only | List products |
| `GET /api/products/{id}` | Inbound only | Get a product |
| `GET /api/users/{id}/profile` | Inbound + Outbound | Fetches from JSONPlaceholder |
| `POST /api/orders` | Inbound + Outbound | Creates order, fetches reviews |

### Record with outbound capture

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -H "X-Witness-Mode: record" \
  -d '{"productId": 1, "quantity": 2}'

# Response includes X-Witness-Id header with the recording ID
```

### Replay with stubbed outbound calls

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -H "X-Witness-Mode: replay" \
  -H "X-Witness-Id: {id-from-record}" \
  -d '{"productId": 1, "quantity": 2}'

# Outbound calls return recorded responses — no real HTTP calls made
```

## Integrating Witness.AspNetCore in your own API

### 1. Add the project reference

```xml
<ProjectReference Include="path/to/Witness.AspNetCore/Witness.AspNetCore.csproj" />
```

### 2. Register outbound capture on your HttpClient

```csharp
builder.Services.AddHttpClient("my-api")
    .AddWitnessCapture(opt =>
    {
        opt.SessionId = "my-session";
        opt.Tag = "outbound";
    });
```

### 3. Add the record/replay middleware

```csharp
app.UseWitnessMiddleware(opt => opt.StorePath = "./witness-store");
```

Now your API supports:
- `X-Witness-Mode: record` — captures all outbound calls with the inbound interaction
- `X-Witness-Mode: replay` + `X-Witness-Id` — stubs outbound calls with recorded responses

## Storage

Interactions are stored in `./witness-store/`:

```
witness-store/
└── sessions/
    └── {sessionId}/
        ├── session.json
        └── interactions/
            └── {witnessId}.json    # includes OutboundCalls when recorded
```

## Troubleshooting

### Server won't start
1. Verify .NET 9.0 SDK is installed: `dotnet --version`
2. Build first: `dotnet build src/Witness.slnx`
3. Check the path in your MCP client configuration

### Tool calls fail
1. Check network connectivity to the target API
2. Verify the target URL is accessible
3. Check the error message in the response

### Outbound capture not working
1. Ensure `AddWitnessCapture()` is registered on the HttpClient
2. Ensure `UseWitnessMiddleware()` is in the pipeline
3. Verify `X-Witness-Mode: record` header is sent

## Next steps

- [Full specification](witness-mcp-server-spec.md) — Complete tool definitions and data model
- [Usage examples](examples/USAGE.md) — Ready-to-use scenarios
- [.NET README](README-DOTNET.md) — Architecture and development guide
