# Witness

> ⚠️ **Alpha** — under active development. Breaking changes may occur.

**Witness is an MCP server that gives AI agents the ability to record, replay, and compare HTTP API interactions.**

Think of it as a flight recorder for your REST APIs — controlled by your AI agent, not by you. Every request and response is captured as a structured, replayable artifact that can be diffed against any other recording.

Record a request in production — including every outbound HTTP call and its response. Replay it locally with all external dependencies stubbed from the recording. The bug reproduces on the first try. The migration diff is automatic. No test scripts to write, no mocks to maintain.

---

## Why it exists

### Reproduce production bugs — instantly, deterministically

When a bug surfaces in production, reproducing it locally is the hardest part. The request depends on specific data, timing, and responses from external services that don't exist in your dev environment.

With Witness, you **record the failing request in production** — including every outbound HTTP call your API made and the responses it received. Then you **replay it locally** with all external dependencies stubbed from the recording. Same request, same data, same third-party responses. The bug reproduces on the first try, every time.

```
Production (record)                     Dev (replay)
┌──────────────┐                        ┌──────────────┐
│  POST /orders│                        │  POST /orders│
│  + payment API response: 402          │  + payment API → 402 (from recording)
│  + inventory API response: 200        │  + inventory API → 200 (from recording)
│  = 500 Internal Server Error          │  = 500 Internal Server Error ← reproduced
└──────────────┘                        └──────────────┘
```

No mocking frameworks. No test fixtures. No guessing what the external services returned. The recording **is** the repro case.

### Validate API migrations with evidence

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

## Outbound capture and replay

Witness also includes an ASP.NET Core library (`Witness.AspNetCore`) that enables **recording and replaying outbound HTTP calls** made by your API during request processing.

```
┌─── RECORD ──────────────────────────────────────────────┐
│                                                          │
│  Inbound request → API processes it                      │
│    ├─ Outbound call #1 → real HTTP → response captured   │
│    └─ Outbound call #2 → real HTTP → response captured   │
│  All captured as Interaction.OutboundCalls                │
│                                                          │
└──────────────────────────────────────────────────────────┘

┌─── REPLAY ──────────────────────────────────────────────┐
│                                                          │
│  Inbound request → API processes it                      │
│    ├─ Outbound call #1 → intercepted → recorded response │
│    └─ Outbound call #2 → intercepted → recorded response │
│  No real HTTP calls — fully deterministic                │
│                                                          │
└──────────────────────────────────────────────────────────┘
```

This is driven by request headers:
- `X-Witness-Mode: record` — execute outbound calls normally and capture responses
- `X-Witness-Mode: replay` + `X-Witness-Id: {id}` — stub outbound calls with recorded responses

### Integration

```csharp
// Register outbound capture on an HttpClient
builder.Services.AddHttpClient("external-api")
    .AddWitnessCapture(opt => opt.SessionId = "my-session");

// Enable the record/replay middleware
app.UseWitnessMiddleware(opt => opt.StorePath = "./witness-store");
```

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

## Using Witness in a Copilot Chat session

Once Witness is configured as an MCP server in VS Code, you can drive the entire workflow from the Copilot Chat panel without writing a single line of test code.

Here is a real session validating the same order API migration from the example above.

---

**You:**
> I need to validate that the new orders API at http://modern:3002 behaves correctly compared to the legacy one at http://legacy:3001. Start by recording a product lookup and an order creation against the legacy service, then replay both against the new service and tell me what changed.

---

**Copilot:** Sure. I'll record two interactions against the legacy service, replay them against the new one, and compare the results.

*Calling* `witness/record` *— GET /api/products/1 on legacy*
> ✅ 200 OK — `{ "id": 1, "name": "Widget", "unit_price": 9.99, "stock": 100 }`
> WitnessId: `legacy-get-product_GET_api-products-1_00000000_20260320T1125`

*Calling* `witness/record` *— POST /api/orders on legacy*
> ✅ 201 Created — `{ "order_id": 1001, "qty": 2, "total_price": 19.98, "status": "pending" }`
> WitnessId: `legacy-create-order_POST_api-orders_ff3f6f9b_20260320T1125`

*Calling* `witness/replay` *— replaying GET /api/products/1 on modern*
> ✅ 200 OK — `{ "id": 1, "name": "Widget", "price": 9.99, "currency": "USD" }`

*Calling* `witness/replay` *— replaying POST /api/orders on modern*
> ✅ 201 Created — `{ "orderId": 1001, "quantity": 2, "amount": 19.98, "currency": "USD", "state": "created" }`

*Calling* `witness/compare` *— product lookup*
> ❌ Body mismatch (4 diffs): `unit_price` → `price`, `stock` removed, `currency` added

*Calling* `witness/compare` *— order creation*
> ❌ Body mismatch (5 diffs): `order_id` → `orderId`, `qty` → `quantity`, `total_price` → `amount`, `status` removed, `state` + `currency` added

**Both endpoints return the correct HTTP status codes.** The schema changed intentionally between legacy and modern:

| Field (legacy) | Field (modern) | Change |
|---|---|---|
| `unit_price` | `price` | renamed |
| `stock` | *(removed)* | no longer exposed |
| *(none)* | `currency` | new field |
| `order_id` | `orderId` | renamed (camelCase) |
| `qty` | `quantity` | renamed |
| `total_price` | `amount` | renamed |
| `status: "pending"` | `state: "created"` | renamed + new vocabulary |

No data was lost — values are preserved, just under different field names. The modern API also adds `currency` as a new required field. If any consumers depend on the legacy field names, they will need to be updated before you cut over.

---

**You:**
> Good. Save this as session "order-api-migration-v2" and list everything that was recorded.

---

**Copilot:** *Calling* `witness/list` *— session "order-api-migration-v2"*

```
Session: order-api-migration-v2  (6 interactions)

  GET  /api/products/1   200   legacy-get-product_GET_...
  GET  /api/products/1   200   replay-legacy-get-product_GET_...
  POST /api/orders       201   legacy-create-order_POST_...
  POST /api/orders       201   replay-legacy-create-order_POST_...
```

All interactions are stored in `./witness-store/sessions/order-api-migration-v2/`. You can inspect any individual recording with `witness/inspect` or re-run the comparison at any time.

---

That's the full loop — from natural language to structured migration evidence — in one chat session, no code written.

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

### 3. Try the demo API

```bash
cd demo
docker compose up demo-api -d

# The API is now running at http://localhost:5080
# Record an order (with outbound call to JSONPlaceholder):
curl -X POST http://localhost:5080/api/orders \
  -H "Content-Type: application/json" \
  -H "X-Witness-Mode: record" \
  -d '{"productId": 1, "quantity": 2}'
```

### 4. Use it

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

**Production bug reproduction** — Record the failing request in production (with all outbound call responses captured). Replay it in your dev environment — same request, same external responses, bug reproduces on first try. No mocking, no guessing.

**API migration** — Record legacy behavior, replay against the new service, compare. Every difference is visible before you switch traffic.

**Version upgrade validation** — Record against v1, replay against v2. Prove compatibility or surface breaking changes.

**Regression testing** — Record a baseline today. Replay it after your next deploy. Any behavioral change shows up immediately.

**Offline development** — Record interactions with a third-party API once, then replay from recordings. No network, no rate limits, no costs.

**Outbound dependency stubbing** — Record your API's behavior including all outbound HTTP calls. Replay the same request with outbound calls stubbed from recordings. Fully deterministic, offline-capable testing.

---

## Repository layout

```
src/                   .NET 9.0 solution
  Witness.Domain/      Core domain (Interaction, WitnessId, HttpRequest/Response)
  Witness.Application/ CQRS commands and queries (Record, Replay, Inspect, List)
  Witness.Infrastructure/ Storage, HTTP execution
  Witness.AspNetCore/  ASP.NET Core library (outbound capture + record/replay middleware)
  Witness.McpServer/   MCP server host
demo/
  Witness.DemoApi/     Sample API demonstrating inbound + outbound capture
  docker-compose.yml   Docker setup for demo APIs
docs/                  Documentation, spec, quickstart
```

See [docs/QUICKSTART.md](docs/QUICKSTART.md) for a detailed setup guide and [docs/witness-mcp-server-spec.md](docs/witness-mcp-server-spec.md) for the full specification.

---

## License

[Apache-2.0](LICENSE)

---

<p align="center"><i>Every HTTP interaction is evidence. Witness captures it.</i></p>
