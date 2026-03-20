# Witness MCP Server - Usage Examples

This document provides examples of how to use the Witness MCP Server tools.

## Prerequisites

Make sure you have:
1. Built the Witness MCP Server: `dotnet build src/Witness.slnx`
2. Configured it in your MCP client (Claude Desktop, VS Code with GitHub Copilot, etc.)

## Basic Usage

### 1. Record an HTTP Interaction

Record a simple GET request:

```
Agent: Use witness/record to call GET https://jsonplaceholder.typicode.com/posts/1
```

The tool will execute the request and return:
- `witnessId` - Unique identifier for this interaction
- `statusCode` - HTTP response status
- `durationMs` - Request duration
- `responseBody` - Full response body
- `responseHeaders` - Response headers

### 2. Record a POST Request

```
Agent: Use witness/record with:
- target: https://jsonplaceholder.typicode.com
- method: POST
- path: /posts
- body: { "title": "Test Post", "body": "This is a test", "userId": 1 }
- options.tag: "create-post-test"
```

### 3. List Recorded Sessions

```
Agent: Use witness/list to see all recorded sessions
```

This returns all sessions with their metadata.

### 4. List Interactions in a Session

```
Agent: Use witness/list with sessionId: "session-2026-02-08"
```

This shows all interactions within that specific session.

### 5. Inspect an Interaction

```
Agent: Use witness/inspect with witnessId from a previous recording
```

This returns the full interaction details including:
- Complete request (method, URL, headers, body)
- Complete response (status, headers, body, duration)
- Metadata (tags, timestamp, session info)

### 6. Replay an Interaction

```
Agent: Use witness/replay with:
- witnessId: "create-post-test_POST_posts_a1b2c3d4_20260208T1430"
- target: https://jsonplaceholder.typicode.com
```

This replays the exact same request against the specified target.

## Advanced Scenarios

### API Migration Testing

**Step 1: Record against the old API**
```
witness/record:
  target: https://api-v1.example.com
  method: POST
  path: /api/users
  body: { "name": "Alice", "email": "alice@example.com" }
  options:
    tag: "create-user"
    sessionId: "migration-test-2026-02-08"
```

**Step 2: Replay against the new API**
```
witness/replay:
  witnessId: "create-user_POST_api-users_a1b2c3d4_20260208T1430"
  target: https://api-v2.example.com
  options:
    tag: "create-user-v2"
```

**Step 3: Compare (future feature)**
```
witness/compare:
  sourceWitnessId: "create-user_POST_api-users_a1b2c3d4_20260208T1430"
  targetWitnessId: "create-user-v2_POST_api-users_a1b2c3d4_20260208T1445"
```

### Regression Testing

**Record a baseline:**
```
witness/record:
  target: https://api.example.com
  method: GET
  path: /api/products/123
  options:
    tag: "baseline"
    sessionId: "regression-suite"
```

**After code changes, replay and compare:**
```
witness/replay:
  witnessId: "baseline_GET_api-products-123_00000000_20260208T1430"
  target: https://api.example.com
  options:
    tag: "after-changes"
```

### Custom Headers and Authentication

```
witness/record:
  target: https://api.example.com
  method: GET
  path: /api/private/data
  headers:
    Authorization: "Bearer your-token-here"
    X-Custom-Header: "custom-value"
  options:
    tag: "authenticated-request"
```

## Tips

1. **Use descriptive tags** - Tags become part of the WitnessId, making interactions easier to find
2. **Group related tests** - Use the same sessionId for related interactions
3. **Inspect before replay** - Use witness/inspect to verify what you're about to replay
4. **List regularly** - Use witness/list to see what's been recorded and avoid duplicates

## WitnessId Format

WitnessIds are structured as:
```
{tag}_{method}_{path-slug}_{body-hash}_{timestamp}
```

Example:
```
create-user_POST_api-users_a1b2c3d4_20260208T1430
```

Where:
- `create-user` - Your tag
- `POST` - HTTP method
- `api-users` - Path with slashes replaced by hyphens
- `a1b2c3d4` - First 8 chars of request body SHA-256 hash
- `20260208T1430` - Timestamp (YYYYMMDDTHHMM)

## Storage

By default, all interactions are stored in `./witness-store/` with the following structure:

```
witness-store/
├── sessions/
│   └── session-2026-02-08/
│       ├── session.json
│       └── interactions/
│           ├── create-user_POST_api-users_a1b2c3d4_20260208T1430.json
│           └── ...
```

You can configure the storage path in `witness.config.json`.

## Outbound Record/Replay

If your API makes outbound HTTP calls to external services, Witness can capture those calls during recording and stub them during replay.

### Setup

Your API needs `Witness.AspNetCore` integration:

```csharp
// Register outbound capture
builder.Services.AddHttpClient("external-api")
    .AddWitnessCapture(opt => opt.SessionId = "my-session");

// Add record/replay middleware
app.UseWitnessMiddleware(opt => opt.StorePath = "./witness-store");
```

### Record with outbound capture

Send a request with the `X-Witness-Mode: record` header:

```
Agent: Use witness/record to POST http://localhost:5000/api/orders
       with body {"productId": 1, "quantity": 2}
       and header X-Witness-Mode: record
```

Or with curl:
```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -H "X-Witness-Mode: record" \
  -d '{"productId": 1, "quantity": 2}'

# Response header: X-Witness-Id: demo-order_POST_api-orders_e78d058d_20260320T2132
```

The stored interaction now includes an `OutboundCalls` array with all HTTP calls the API made during processing.

### Replay with stubbed outbound calls

Send the same request with `X-Witness-Mode: replay` and the recorded `X-Witness-Id`:

```bash
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -H "X-Witness-Mode: replay" \
  -H "X-Witness-Id: demo-order_POST_api-orders_e78d058d_20260320T2132" \
  -d '{"productId": 1, "quantity": 2}'
```

During replay:
- The API processes the request normally
- When it tries to make outbound HTTP calls, `WitnessCaptureHandler` intercepts them
- Instead of real HTTP calls, the recorded responses are returned from the playback queue
- The result is fully deterministic — no external dependencies needed

### Inspect the recorded outbound calls

```
Agent: Use witness/inspect to view demo-order_POST_api-orders_e78d058d_20260320T2132
```

The interaction JSON includes:
```json
{
  "WitnessId": "demo-order_POST_api-orders_e78d058d_20260320T2132",
  "OutboundCalls": [
    {
      "WitnessId": "outbound_GET_posts-1-comments_00000000_20260320T2132",
      "Request": { "Method": "GET", "Path": "/posts/1/comments" },
      "Response": { "StatusCode": 200, "Body": [...] }
    }
  ]
}
```

### Use cases for outbound record/replay

- **Production bug repro** — Record a failing request in prod, replay locally with all external responses frozen
- **Offline testing** — Record once, replay without network access
- **Deterministic CI** — No flaky tests from external service outages
- **Migration validation** — Record against old API, replay against new, compare responses
- **Performance testing** — Outbound stubs return instantly (< 1ms vs real network latency)

## Production Bug Reproduction

The most powerful Witness use case: when a bug happens in production, record the exact request (including all outbound call responses), then replay it locally to reproduce the issue deterministically.

### Scenario

Your `/api/orders` endpoint returns 500 in production. It depends on an external payment API and an inventory service. You need to reproduce the bug locally, but you can't replicate the exact state of those external services.

### Step 1: Record the failing request in production

```bash
# Hit the production endpoint with recording enabled
curl -X POST https://production.example.com/api/orders \
  -H "Content-Type: application/json" \
  -H "X-Witness-Mode: record" \
  -H "X-Witness-Session: bug-1234" \
  -H "X-Witness-Tag: repro" \
  -d '{"productId": 42, "quantity": 1}'

# Response: 500 Internal Server Error
# Header: X-Witness-Id: repro_POST_api-orders_e78d058d_20260320T1530
```

The recording now contains:
- The inbound request and the 500 response
- **All outbound calls**: the payment API returned `402 Payment Required`, the inventory service returned `200 OK` with `stock: 0`
- The exact response bodies, headers, and timing

### Step 2: Transfer the recording

```bash
# Copy from production store to local store
scp prod:/var/witness-store/sessions/bug-1234/interactions/repro_*.json \
    ./witness-store/sessions/bug-1234/interactions/
```

Or if using shared storage / volume mounts, the recording is already accessible.

### Step 3: Replay locally with a debugger attached

```bash
# Start your API locally with a debugger
dotnet run --project src/MyApi

# Replay the exact production request
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -H "X-Witness-Mode: replay" \
  -H "X-Witness-Id: repro_POST_api-orders_e78d058d_20260320T1530" \
  -d '{"productId": 42, "quantity": 1}'
```

What happens:
1. Your local API receives the same POST request
2. When it calls the payment API → Witness returns the recorded `402` response (no real HTTP call)
3. When it calls the inventory service → Witness returns the recorded `200` with `stock: 0`
4. The same 500 error occurs — you can now step through the code and find the bug

### Step 4: Fix and verify

After fixing the bug, replay the same recording again:

```bash
# Same replay — should now return 200 or a proper error instead of 500
curl -X POST http://localhost:5000/api/orders \
  -H "Content-Type: application/json" \
  -H "X-Witness-Mode: replay" \
  -H "X-Witness-Id: repro_POST_api-orders_e78d058d_20260320T1530" \
  -d '{"productId": 42, "quantity": 1}'
```

The recording becomes a **permanent regression test** — you can replay it after every deploy to verify the bug stays fixed.

### Why this is better than traditional debugging

| Traditional approach | Witness approach |
|---|---|
| "What did the payment API return?" — check logs, hope they're detailed enough | Payment API response is in the recording |
| Set up mock services with guessed data | External responses are the real production data |
| Bug doesn't repro locally because external services return different data | Exact same responses, every time |
| Spend hours building a test fixture | Copy one JSON file |
| Repro depends on production state that changes | Recording is immutable — works forever |
