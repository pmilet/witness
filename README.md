# Witness

> ⚠️ **Alpha** — under active development. Breaking changes may occur.

**Witness is an MCP server that gives AI agents the ability to record, replay, and compare HTTP API interactions.**

Think of it as a flight recorder for your REST APIs — controlled by your AI agent, not by you. Every request and response is captured as a structured, replayable artifact that can be diffed against any other recording. The result is machine-readable proof that two systems behave identically.

---

## Why it exists

Testing API migrations, version upgrades, and backend replacements is tedious and error-prone when done manually. Witness automates the evidence-gathering:

- Record the behavior of the **old** system.
- Replay the same requests against the **new** system.
- Compare the responses — field by field.

No test scripts to write. No assertions to maintain. The AI agent drives the whole workflow through four tool calls.

---

## How it works

Witness runs as an MCP server that your AI agent (Claude, Copilot, etc.) connects to. The agent calls Witness tools the same way it calls any other tool — by describing what it wants to do in natural language. Witness handles the HTTP execution, storage, and diffing.

```
┌──────────────┐        MCP tools        ┌─────────────────────────┐
│   AI Agent   │ ──────────────────────► │    Witness MCP Server   │
│ (Claude, etc)│                         │                         │
└──────────────┘                         │  witness/record  ──────►│──► Legacy API
                                         │  witness/replay  ──────►│──► Modern API
                                         │  witness/compare ───────│
                                         │  witness/list    ───────│
                                         │  witness/inspect ───────│
                                         │                         │
                                         │  Interaction Store      │
                                         │  witness-store/         │
                                         │    sessions/            │
                                         │      {session}/         │
                                         │        interactions/    │
                                         └─────────────────────────┘
```

Every recorded interaction is saved as a JSON file and assigned a human-readable `WitnessId`:

```
legacy-create-order_POST_api-orders_ff3f6f9b_20260320T1125
└──── tag ────┘ └─method─┘ └──path──┘ └body hash┘ └timestamp┘
```

The same request always produces the same ID, which makes recordings referenceable and deduplicated.

---

## Example: validating an API migration

Say you are migrating an orders API from a legacy system to a modern one. The schemas changed — `order_id` became `orderId`, `status: "pending"` became `state: "created"` — but both endpoints accept the same request and return HTTP 201.

You want proof that the new API handles requests correctly before switching traffic.

### 1. Ask your AI agent

> "Record a POST to /api/orders against the legacy service at http://legacy:3001, then replay it against the new service at http://modern:3002, and compare the responses."

### 2. The agent calls Witness tools

**Record against legacy:**
```json
witness/record
{
  "target": "http://legacy:3001",
  "method": "POST",
  "path": "/api/orders",
  "body": { "product_id": 1, "qty": 2 },
  "options": { "tag": "create-order", "sessionId": "migration-validation" }
}
```
```json
{
  "WitnessId": "create-order_POST_api-orders_ff3f6f9b_20260320T1125",
  "StatusCode": 201,
  "ResponseBody": { "order_id": 1001, "qty": 2, "total_price": 19.98, "status": "pending" }
}
```

**Replay against the new service:**
```json
witness/replay
{
  "witnessId": "create-order_POST_api-orders_ff3f6f9b_20260320T1125",
  "target": "http://modern:3002",
  "options": { "sessionId": "migration-validation" }
}
```
```json
{
  "ReplayWitnessId": "replay-create-order_POST_api-orders_ff3f6f9b_20260320T1125",
  "StatusCode": 201,
  "ResponseBody": { "orderId": 1001, "quantity": 2, "amount": 19.98, "currency": "USD", "state": "created" }
}
```

**Compare:**
```json
witness/compare
{
  "witnessId1": "create-order_POST_api-orders_ff3f6f9b_20260320T1125",
  "witnessId2": "replay-create-order_POST_api-orders_ff3f6f9b_20260320T1125"
}
```
```json
{
  "isMatch": false,
  "summary": {
    "statusCode": { "match": true, "original": 201, "replay": 201 },
    "body": {
      "match": false,
      "diffCount": 5,
      "diffs": [
        { "path": "order_id",    "original": 1001,      "replay": null },
        { "path": "orderId",     "original": null,      "replay": 1001 },
        { "path": "status",      "original": "pending", "replay": null },
        { "path": "state",       "original": null,      "replay": "created" },
        { "path": "currency",    "original": null,      "replay": "USD" }
      ]
    }
  }
}
```

The agent reports back: *"Both services return 201. The schema changed as expected — `order_id` → `orderId`, `status` → `state`, and the new service adds a `currency` field. No data was lost."*

You now have a structured, reproducible record of exactly what changed. No manual comparison, no guessing.

### 3. List all recorded interactions in the session

```json
witness/list
{ "sessionId": "migration-validation" }
```
```json
{
  "SessionId": "migration-validation",
  "Count": 2,
  "Interactions": [
    { "WitnessId": "create-order_POST_...", "Method": "POST", "StatusCode": 201 },
    { "WitnessId": "replay-create-order_POST_...", "Method": "POST", "StatusCode": 201 }
  ]
}
```

---

## Getting started

### 1. Build

```bash
git clone https://github.com/pmilet/witness.git
cd witness
dotnet build src/Witness.slnx
```

### 2. Configure your MCP client

**Claude Desktop / Claude Code** — add to `claude_desktop_config.json` or `.claude/settings.json`:
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

**VS Code (GitHub Copilot)** — add to `.vscode/mcp.json`:
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

### 3. Use it

Recordings are stored in `./witness-store/` relative to where the server runs. Sessions group related interactions. Point your agent at any HTTP API and start recording.

---

## Available tools

| Tool | What it does |
|------|-------------|
| `witness/record` | Execute an HTTP request and save the full interaction |
| `witness/replay` | Re-send a recorded request to a different target |
| `witness/compare` | Diff two recorded interactions field by field |
| `witness/list` | Browse sessions and their interactions |
| `witness/inspect` | View the full details of a single interaction |

---

## Common use cases

**API migration** — Record legacy behavior, replay against the new service, compare. Every difference is visible before you switch traffic.

**Version upgrade validation** — Record against v1, replay against v2. Prove compatibility or surface breaking changes.

**Regression testing** — Record a baseline today. Replay it after your next deploy. Any behavioral change shows up immediately.

**Offline development** — Record interactions with a third-party API once, then replay from recordings. No network, no rate limits, no costs.

---

## Repository layout

```
src/    .NET 9.0 solution (Domain, Application, Infrastructure, McpServer)
demo/   Simulation test with two Docker APIs illustrating a migration scenario
docs/   Full documentation, spec, quickstart, and usage examples
```

See [docs/QUICKSTART.md](docs/QUICKSTART.md) for a detailed setup guide and [docs/witness-mcp-server-spec.md](docs/witness-mcp-server-spec.md) for the full specification.

---

## License

[Apache-2.0](LICENSE)

---

<p align="center"><i>Every HTTP interaction is evidence. Witness captures it.</i></p>
