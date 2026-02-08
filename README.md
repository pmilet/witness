# Witness

> **⚠️ ALPHA VERSION WARNING**  
> This software is currently in **alpha stage** and under active development.  
> Features may change, and breaking changes may occur without notice.  
> Use in production environments at your own risk.

**An MCP server that gives AI agents the power to record, replay, and compare HTTP API interactions.**

The REST equivalent of [Playwright MCP](https://github.com/microsoft/playwright-mcp) — but for APIs instead of browsers.

> **🎉 .NET 9.0 Version Available!**  
> This repository now includes a complete .NET 9.0 implementation with Domain-Driven Design, Clean Code principles, and comprehensive unit tests.  
> See **[README-DOTNET.md](README-DOTNET.md)** for the .NET version documentation.
>
> **Migration Path:**
> - **TypeScript (Original)**: Production-ready, fully functional (see below)
> - **.NET 9.0 (New)**: Modern architecture, extensible, fully tested (see [README-DOTNET.md](README-DOTNET.md))
> - Both versions use the same storage format for seamless interoperability

---

## What It Does

Witness lets AI agents interact with REST APIs the way a senior QA engineer would:

```
Agent → witness/discover  → "What endpoints does this API have?"
Agent → witness/record    → "Call POST /api/loans and save what happens"
Agent → witness/replay    → "Send that same request to the new service"
Agent → witness/compare   → "Are the responses identical?"
```

Every HTTP interaction becomes a **replayable, comparable, shareable artifact** — identified by a structured `WitnessId` that tells you exactly what it is at a glance.

```
mortgage-happy-path_POST_api-loans_a3f7b2c1_20260208T1430
```

## Why

Because "it works on my machine" isn't evidence. Witness produces structured proof that two systems behave identically — whether you're migrating APIs, upgrading versions, swapping databases, or replacing a legacy mainframe with cloud-native services.

## Origin

Witness is the evolution of [pmilet/playback](https://github.com/pmilet/playback), an ASP.NET Core middleware for recording and replaying HTTP requests (2017). The core mental model is preserved — record, replay, compare — but control shifts from human-set headers to AI agent tool invocations via [MCP](https://modelcontextprotocol.io/).

---

## Quick Start

### Install

```bash
git clone https://github.com/pmilet/witness.git
cd witness
npm install
npm run build
```

See [QUICKSTART.md](QUICKSTART.md) for detailed installation and configuration instructions.

### Add to your MCP client

**Claude Desktop / Claude Code:**
```json
{
  "mcpServers": {
    "witness": {
      "command": "node",
      "args": ["/absolute/path/to/witness/dist/index.js"]
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
        "command": "node",
        "args": ["/absolute/path/to/witness/dist/index.js"]
      }
    }
  }
}
```

### Use

Once configured, your AI agent has access to the full tool suite. Just ask:

> "Discover the endpoints at https://api.example.com/swagger/v1/swagger.json, then generate and run a smoke test suite."

> "Record a POST to /api/loans with a mortgage payload against the legacy service, then replay it against the new service and compare the responses."

> "Run the regression suite against staging and tell me if anything changed."

---

## MCP Tools

| Tool | Purpose |
|------|---------|
| `witness/discover` | Parse an OpenAPI spec and list available endpoints |
| `witness/record` | Execute an HTTP request and capture the full interaction |
| `witness/replay` | Replay a recorded interaction against a different target |
| `witness/compare` | Diff two recordings with configurable tolerances |
| `witness/chain` | Execute multi-step flows with variable extraction between steps |
| `witness/generate` | AI-assisted test scenario generation from OpenAPI specs |
| `witness/suite-run` | Batch-execute a test suite with parallel support |
| `witness/mock` | Serve recorded responses as a local mock server |
| `witness/list` | Browse and filter recorded sessions |
| `witness/inspect` | View full details of a recorded interaction |

---

## Examples

### Record and replay

```
# Record against the current service
witness/record → POST /api/accounts { "name": "Alice", "type": "savings" }
  → WitnessId: smoke_POST_api-accounts_b7c1d2e3_20260208T1500
  → 201 Created { "accountId": "A-001", "status": "active" }

# Replay against the new version
witness/replay → same request against https://v2.api.example.com
  → 201 Created { "accountId": "A-001", "status": "active" }

# Compare
witness/compare → source vs target
  → ✅ Match. Responses are functionally identical. Target is 15% faster.
```

### Multi-step flow

```
witness/chain →
  Step 1: POST /api/loans { type: "mortgage", amount: 250000 }
          → extract loanId from response
  Step 2: GET /api/loans/{loanId}/status
          → assert status = "PENDING_REVIEW"
  Step 3: POST /api/loans/{loanId}/approve
          → assert 200 OK
  Step 4: GET /api/loans/{loanId}/status
          → assert status = "APPROVED"

  → ✅ 4/4 steps passed in 892ms
```

### Mock dependencies

```
# Record interactions with a third-party API
witness/record → multiple calls to https://credit-check-api.example.com

# Start mock server from recordings
witness/mock → http://localhost:8081 serving 12 recorded endpoints

# Develop offline against realistic responses
```

---

## Use Cases

### For Developers
- **Pre-commit regression** — Record before your change, replay after, compare. Instant confidence.
- **Offline development** — Mock third-party APIs from recordings. No network, no costs, no rate limits.
- **API exploration** — Discover + generate + chain to systematically learn an unfamiliar API.

### For QA & Testing
- **Contract testing** — Record consumer usage patterns. Replay after provider deploys. Breaking changes surface automatically.
- **Multi-environment validation** — Same suite across dev, staging, prod. Compare to catch environment drift.
- **Chaos testing** — Mock with injected faults (500s, timeouts, malformed JSON) to test resilience.

### For API Migrations
- **Version upgrade validation** — Record against v1, replay against v2, compare. Compatibility proven.
- **Database swap** — Postgres to CosmosDB? Record API behavior, swap backend, replay, compare. Responses should be identical.
- **Legacy displacement** — Record legacy system behavior, replay against the modernized replacement. Every difference is visible.

### For Legacy Modernization
- **Strangler Fig verification** — Prove each endpoint cutover produces identical behavior before going live.
- **Anti-Corruption Layer testing** — Validate that translation between old and new models is correct in both directions.
- **DDD Bubble Context validation** — Verify the bubble's external contracts remain stable as it expands.
- **Migration wave evidence** — Each wave produces a structured parity report as go/no-go evidence for stakeholders.

See the [full specification](docs/SPEC.md) for detailed modernization patterns including event-driven transitions, saga validation, and domain event confirmation.

---

## How It Works

```
┌──────────────────────────────────────────────────────┐
│                 Witness MCP Server                     │
│                                                        │
│  Agent ──► MCP Tools ──► HTTP Executor ──► Target API  │
│                              │                         │
│                              ▼                         │
│                     Interaction Store                   │
│                    (JSON files / Blob)                  │
│                              │                         │
│                    ┌─────────┴─────────┐               │
│                    ▼                   ▼               │
│              Diff Engine         Mock Server            │
│           (compare pairs)    (serve recordings)         │
│                                                        │
└──────────────────────────────────────────────────────┘
```

**Storage:** Interactions are stored as structured JSON files organized by session. Default is local filesystem; Azure Blob Storage is supported for team/CI scenarios.

**WitnessId:** Every recording gets a deterministic, human-readable identifier: `{tag}_{method}_{path-slug}_{body-hash}_{timestamp}`. Same request produces same ID, enabling deduplication.

**Comparison:** The diff engine supports field-level ignoring (timestamps, request IDs), numeric tolerances, array order independence, and severity classification (error/warning/info).

---

## Configuration

```json
{
  "witness": {
    "storage": {
      "type": "local",
      "path": "./witness-store"
    },
    "defaults": {
      "timeoutMs": 30000,
      "followRedirects": true
    },
    "comparison": {
      "defaultIgnoreFields": ["timestamp", "requestId", "date"],
      "defaultNumericTolerance": 0.001
    }
  }
}
```

See [Configuration Reference](docs/SPEC.md#7-configuration) for all options.

---

## Documentation

- [Quick Start Guide](QUICKSTART.md) — Installation, configuration, and first steps
- [Full Specification](witness-mcp-server-spec.md) — Complete tool definitions, data model, architecture, and modernization patterns
- [Usage Examples](examples/USAGE.md) — Ready-to-use scenarios for common use cases
- [Development Guide](DEVELOPMENT.md) — Contributing and development workflow

---

## Roadmap

- [x] Core: record, replay, inspect, list ✅ **v0.1.0 Complete**
- [ ] Chains: multi-step flows with variable extraction
- [ ] Compare: diff engine with configurable tolerances
- [ ] OpenAPI: discover + schema validation
- [ ] Generate: AI-assisted scenario generation
- [ ] Suites: batch execution with parallel support
- [ ] Mock: serve recorded responses
- [ ] Witness Studio: VS Code extension for visual diff and suite management
- [ ] Storage: Azure Blob adapter
- [ ] Telemetry: OpenTelemetry trace correlation

---

## Contributing

Contributions are welcome! Please read [DEVELOPMENT.md](DEVELOPMENT.md) for the development guide.

To get started:
```bash
git clone https://github.com/pmilet/witness.git
cd witness
npm install
npm run build
npm test
```

## License

[Apache-2.0](LICENSE)

---

<p align="center">
  <i>Every HTTP interaction is evidence. Witness captures it.</i>
</p>
