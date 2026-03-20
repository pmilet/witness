# Witness MCP Server

## An Agent-Driven HTTP Record, Replay & Compare Tool

**Version**: 2.0
**Date**: February 2026
**Origin**: Evolved from [playback](https://github.com/pmilet/playback) → [witness](https://github.com/pmilet/witness)
**License**: Apache-2.0

---

## 1. What Is This

Witness MCP Server gives AI agents the ability to interact with REST APIs the way a senior QA engineer would: discover endpoints, execute requests, record interactions, replay them against different targets, compare responses, build regression suites, and mock dependencies — all through MCP tools.

It is the **REST equivalent of Playwright MCP Server**, but for APIs instead of browsers.

### 1.1 Origin Story

The original `pmilet/playback` was an ASP.NET Core middleware that recorded HTTP interactions (inbound and outbound) and replayed them via a structured `WitnessId`. It was controlled by headers: `X-Witness-Mode: Record | Replay | None`.

This redesign preserves the core mental model — record, replay, compare — but shifts control from human-set headers to AI agent tool invocations, adds OpenAPI awareness, introduces response diffing, and stores interactions as structured data rather than opaque blobs.

### 1.2 What Changed

| Original (2017) | Redesign (2026) |
|-----------------|----------------|
| Human sets `X-Witness-Mode` headers | Agent invokes MCP tools |
| Records single request/response pairs | Records full interaction chains (multi-step flows) |
| Blob/file storage | Structured storage (JSON files, optional database) |
| No API awareness | OpenAPI-spec-aware (auto-discovers endpoints, validates schemas) |
| No comparison | Built-in response diffing with configurable tolerances |
| Inbound middleware only for recording | Client-side recording (agent drives requests directly) |
| Outbound recording via HttpClientFactory | Proxy-based outbound capture for dependency recording |
| No test generation | AI-assisted scenario generation from OpenAPI specs |
| .NET only | Language-agnostic MCP server (targets any HTTP API) |

---

## 2. Architecture

### 2.1 Component Overview

```
┌─────────────────────────────────────────────────────────┐
│                  Witness MCP Server                     │
│                                                          │
│  MCP Tools (agent-facing):                               │
│  ┌────────────────────────────────────────────────────┐  │
│  │  witness/discover      Parse OpenAPI spec          │  │
│  │  witness/record        Execute + capture           │  │
│  │  witness/replay        Replay against target       │  │
│  │  witness/compare       Diff two recordings         │  │
│  │  witness/chain         Multi-step flow execution   │  │
│  │  witness/generate      AI scenario generation      │  │
│  │  witness/suite-run     Batch execution             │  │
│  │  witness/mock          Serve recorded responses    │  │
│  │  witness/list          Browse recorded sessions    │  │
│  │  witness/inspect       View interaction details    │  │
│  └────────────────────────────────────────────────────┘  │
│                                                          │
│  Core Engine:                                            │
│  ┌────────────────────────────────────────────────────┐  │
│  │  HttpExecutor         Execute requests + capture    │  │
│  │  InteractionStore     Persist recordings            │  │
│  │  DiffEngine           Compare responses             │  │
│  │  MockServer           Serve recorded responses      │  │
│  │  SchemaValidator      Validate against OpenAPI      │  │
│  │  ChainExecutor        Multi-step flow orchestration │  │
│  └────────────────────────────────────────────────────┘  │
│                                                          │
│  Storage:                                                │
│  ┌────────────────────────────────────────────────────┐  │
│  │  Local filesystem (default)                         │  │
│  │  Azure Blob Storage (optional)                      │  │
│  │  SQLite (optional, for indexing + search)           │  │
│  └────────────────────────────────────────────────────┘  │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

### 2.2 Interaction Model

```
Agent                    Witness MCP Server              Target API
  │                              │                            │
  │── witness/discover ────────►│                            │
  │   (OpenAPI URL)              │──── GET /swagger.json ────►│
  │                              │◄─── spec ─────────────────│
  │◄── endpoint catalog ────────│                            │
  │                              │                            │
  │── witness/record ──────────►│                            │
  │   (method, path, body)       │──── POST /api/loans ──────►│
  │                              │◄─── 200 {loanId:L01} ─────│
  │◄── witnessId + response ───│                            │
  │                              │                            │
  │── witness/replay ──────────►│          Different Target   │
  │   (witnessId, new target)   │──── POST /api/loans ──────►│ (v2)
  │                              │◄─── 200 {loanId:L01} ─────│
  │◄── response ────────────────│                            │
  │                              │                            │
  │── witness/compare ─────────►│                            │
  │   (recording A vs B)         │                            │
  │◄── diff result ─────────────│                            │
```

---

## 3. Data Model

### 3.1 Interaction Record

Every recorded HTTP interaction produces an `Interaction` record:

```typescript
interface Interaction {
    // Identity
    witnessId: string;             // Structured unique ID
    sessionId: string;              // Groups related interactions
    timestamp: string;              // ISO 8601

    // Request
    request: {
        method: string;             // GET, POST, PUT, DELETE, PATCH
        url: string;                // Full URL including query params
        path: string;               // Path only (for matching)
        headers: Record<string, string>;
        body?: any;                 // Parsed JSON or raw string
        contentType?: string;
    };

    // Response
    response: {
        statusCode: number;
        headers: Record<string, string>;
        body?: any;
        contentType?: string;
        durationMs: number;
    };

    // Metadata
    metadata: {
        tags: string[];             // User-defined tags
        description?: string;       // What this interaction tests
        openApiOperationId?: string; // Linked OpenAPI operation
        chainStep?: number;         // Position in a multi-step chain
        chainId?: string;           // Groups chained interactions
    };

    // Outbound calls (if proxy capture enabled)
    outboundCalls?: Interaction[];
}
```

### 3.2 WitnessId Format

Preserving the spirit of the original structured WitnessId, but simplified and extensible:

```
{tag}_{method}_{path-slug}_{body-hash}_{timestamp}

Examples:
  smoke_POST_api-loans_a3f7b2c1_20260208T1430
  regression_GET_api-accounts-123_00000000_20260208T1431
  load-test_PUT_api-customers-456_b8e2d1f3_20260208T1432
```

Components:
- **tag**: Agent-provided label (test suite name, scenario name, or auto-generated)
- **method**: HTTP verb
- **path-slug**: URL path with slashes replaced by hyphens, truncated to 60 chars
- **body-hash**: First 8 chars of SHA-256 of request body (00000000 for no body)
- **timestamp**: Compact ISO timestamp

The WitnessId is both human-readable (you can see what it is at a glance) and deterministic (same request produces same ID, enabling deduplication).

### 3.3 Storage Layout

```
witness-store/
  ├── sessions/
  │   ├── {sessionId}/
  │   │   ├── session.json           # Session metadata
  │   │   ├── interactions/
  │   │   │   ├── {witnessId}.json  # Full interaction record
  │   │   │   └── ...
  │   │   └── comparisons/
  │   │       ├── {comparisonId}.json
  │   │       └── ...
  │   └── ...
  ├── suites/
  │   ├── {suiteName}.json           # Test suite definition
  │   └── ...
  ├── mocks/
  │   ├── {mockProfile}.json         # Mock server configuration
  │   └── ...
  └── specs/
      ├── {apiName}-openapi.json     # Cached OpenAPI specs
      └── ...
```

---

## 4. MCP Tool Specifications

### 4.1 witness/discover

Parse an OpenAPI specification and register available endpoints.

**Input:**
```json
{
    "source": "https://api.example.com/swagger/v1/swagger.json",
    "filter": {
        "tags": ["loans", "accounts"],
        "methods": ["GET", "POST"]
    }
}
```

`source` can be a URL or a local file path.

**Output:**
```json
{
    "apiName": "Lending Service",
    "baseUrl": "https://api.example.com",
    "version": "1.0.0",
    "endpoints": [
        {
            "operationId": "createLoan",
            "method": "POST",
            "path": "/api/loans",
            "summary": "Create a new loan application",
            "parameters": [],
            "requestBody": {
                "contentType": "application/json",
                "schema": { "type": "object", "properties": { "loanType": { "type": "string" }, "amount": { "type": "number" } } },
                "required": ["loanType", "amount"]
            },
            "responses": {
                "200": { "description": "Loan created", "schema": { "$ref": "#/components/schemas/Loan" } },
                "400": { "description": "Validation error" }
            }
        },
        {
            "operationId": "getLoanStatus",
            "method": "GET",
            "path": "/api/loans/{loanId}/status",
            "summary": "Get loan application status",
            "parameters": [{ "name": "loanId", "in": "path", "required": true, "type": "string" }],
            "responses": {
                "200": { "description": "Loan status" },
                "404": { "description": "Loan not found" }
            }
        }
    ],
    "totalEndpoints": 2
}
```

**Use cases:**
- Agent explores an unfamiliar API before testing
- Auto-generate test scenarios from discovered endpoints
- Validate that a modernized API exposes the same operations as the original

---

### 4.2 witness/record

Execute an HTTP request against a target and capture the full interaction.

**Input:**
```json
{
    "target": "https://api.example.com",
    "method": "POST",
    "path": "/api/loans",
    "headers": {
        "Authorization": "Bearer {{token}}",
        "Content-Type": "application/json"
    },
    "body": {
        "loanType": "MORTGAGE",
        "amount": 250000,
        "term": 360
    },
    "options": {
        "tag": "mortgage-happy-path",
        "sessionId": "regression-2026-02-08",
        "validateSchema": true,
        "captureOutbound": false,
        "followRedirects": true,
        "timeoutMs": 30000
    }
}
```

**Output:**
```json
{
    "witnessId": "mortgage-happy-path_POST_api-loans_c4a8e1b2_20260208T1430",
    "sessionId": "regression-2026-02-08",
    "statusCode": 200,
    "durationMs": 234,
    "responseBody": {
        "loanId": "L-20260208-001",
        "status": "PENDING_REVIEW",
        "estimatedCloseDate": "2026-04-15"
    },
    "responseHeaders": {
        "content-type": "application/json",
        "x-request-id": "req-abc-123"
    },
    "schemaValid": true,
    "stored": true
}
```

**Variables:** The `{{token}}` syntax in headers and body supports variable substitution from:
- Environment variables
- Previous chain step responses (see `witness/chain`)
- Session-level variables set by the agent

---

### 4.3 witness/replay

Replay a previously recorded request against a (potentially different) target.

**Input:**
```json
{
    "witnessId": "mortgage-happy-path_POST_api-loans_c4a8e1b2_20260208T1430",
    "target": "https://modernized-api.example.com",
    "options": {
        "tag": "modernized-replay",
        "overrideHeaders": {
            "Authorization": "Bearer {{new-token}}"
        },
        "mockOutbound": false
    }
}
```

**Output:**
```json
{
    "originalWitnessId": "mortgage-happy-path_POST_api-loans_c4a8e1b2_20260208T1430",
    "replayWitnessId": "modernized-replay_POST_api-loans_c4a8e1b2_20260208T1445",
    "statusCode": 200,
    "durationMs": 187,
    "responseBody": {
        "loanId": "L-20260208-001",
        "status": "PENDING_REVIEW",
        "estimatedCloseDate": "2026-04-15"
    },
    "stored": true
}
```

**Use cases:**
- Replay production-recorded interactions against a staging environment
- Replay legacy service recordings against a modernized replacement
- Replay against the same service after a code change (regression)

---

### 4.4 witness/compare

Compare two recorded interactions and produce a structured diff.

**Input:**
```json
{
    "sourceWitnessId": "mortgage-happy-path_POST_api-loans_c4a8e1b2_20260208T1430",
    "targetWitnessId": "modernized-replay_POST_api-loans_c4a8e1b2_20260208T1445",
    "options": {
        "ignoreFields": ["timestamp", "requestId", "x-request-id", "date"],
        "ignoreHeaders": true,
        "numericTolerance": 0.01,
        "arrayOrderMatters": false,
        "deepCompare": true
    }
}
```

**Output:**
```json
{
    "match": true,
    "statusCodeMatch": true,
    "bodyMatch": true,
    "differences": [],
    "ignoredDifferences": [
        {
            "path": "$.loanId",
            "source": "L-20260208-001",
            "target": "L-20260208-001",
            "type": "identical"
        }
    ],
    "performance": {
        "sourceDurationMs": 234,
        "targetDurationMs": 187,
        "deltaMs": -47,
        "improvement": "20.1%"
    },
    "summary": "Responses are functionally identical. Target is 20% faster."
}
```

When differences exist:
```json
{
    "match": false,
    "statusCodeMatch": true,
    "bodyMatch": false,
    "differences": [
        {
            "path": "$.estimatedCloseDate",
            "source": "2026-04-15",
            "target": "2026-04-22",
            "type": "value_mismatch",
            "severity": "warning"
        },
        {
            "path": "$.interestRate",
            "source": 6.75,
            "target": 6.76,
            "type": "numeric_within_tolerance",
            "tolerance": 0.01,
            "severity": "info"
        }
    ],
    "summary": "1 meaningful difference found: estimatedCloseDate differs by 7 days."
}
```

**Diff severity levels:**
- `error`: Status code mismatch, missing fields, type changes
- `warning`: Value differences in business-significant fields
- `info`: Differences within configured tolerance or in non-critical fields

---

### 4.5 witness/chain

Execute a multi-step interaction flow where each step can reference outputs from previous steps.

**Input:**
```json
{
    "target": "https://api.example.com",
    "sessionId": "loan-lifecycle-test",
    "tag": "full-lifecycle",
    "steps": [
        {
            "name": "Create Loan",
            "method": "POST",
            "path": "/api/loans",
            "body": { "loanType": "MORTGAGE", "amount": 250000 },
            "extract": {
                "loanId": "$.loanId"
            }
        },
        {
            "name": "Check Status",
            "method": "GET",
            "path": "/api/loans/{{loanId}}/status",
            "assert": {
                "statusCode": 200,
                "body.status": "PENDING_REVIEW"
            }
        },
        {
            "name": "Approve Loan",
            "method": "POST",
            "path": "/api/loans/{{loanId}}/approve",
            "body": { "approvedBy": "agent-test", "conditions": [] },
            "assert": {
                "statusCode": 200
            }
        },
        {
            "name": "Verify Approved",
            "method": "GET",
            "path": "/api/loans/{{loanId}}/status",
            "assert": {
                "body.status": "APPROVED"
            }
        }
    ]
}
```

**Output:**
```json
{
    "chainId": "chain_full-lifecycle_20260208T1430",
    "sessionId": "loan-lifecycle-test",
    "totalSteps": 4,
    "passedSteps": 4,
    "failedSteps": 0,
    "totalDurationMs": 892,
    "steps": [
        {
            "name": "Create Loan",
            "witnessId": "full-lifecycle_POST_api-loans_c4a8e1b2_20260208T1430",
            "statusCode": 200,
            "durationMs": 234,
            "extracted": { "loanId": "L-20260208-001" },
            "assertionsPassed": true
        },
        {
            "name": "Check Status",
            "witnessId": "full-lifecycle_GET_api-loans-L-20260208-001-status_00000000_20260208T1430",
            "statusCode": 200,
            "durationMs": 45,
            "assertionsPassed": true
        },
        {
            "name": "Approve Loan",
            "witnessId": "full-lifecycle_POST_api-loans-L-20260208-001-approve_d1e2f3a4_20260208T1430",
            "statusCode": 200,
            "durationMs": 567,
            "assertionsPassed": true
        },
        {
            "name": "Verify Approved",
            "witnessId": "full-lifecycle_GET_api-loans-L-20260208-001-status_00000000_20260208T1431",
            "statusCode": 200,
            "durationMs": 46,
            "assertionsPassed": true
        }
    ],
    "allInteractionsStored": true
}
```

**Key features:**
- `extract` captures values from responses using JSONPath, making them available as `{{variable}}` in subsequent steps
- `assert` validates response properties inline (fail-fast if assertions fail)
- `retryUntil` polls an endpoint until a condition is met (essential for async/event-driven flows)
- `delay` pauses between steps (for eventual consistency scenarios)
- Every step is individually recorded and replayable
- The entire chain is replayable as a unit via `witness/replay` with `chainId`

**Retry/polling options per step:**
```json
{
    "options": {
        "retryUntil": { "body.status": "APPROVED" },
        "maxRetries": 20,
        "retryDelayMs": 500,
        "retryTimeoutMs": 30000
    }
}
```

---

### 4.6 witness/generate

AI-assisted test scenario generation from an OpenAPI spec or from recorded interaction patterns.

**Input:**
```json
{
    "source": "openapi",
    "openApiUrl": "https://api.example.com/swagger/v1/swagger.json",
    "strategy": "crud-lifecycle",
    "options": {
        "includeErrorCases": true,
        "includeEdgeCases": true,
        "maxScenariosPerEndpoint": 3
    }
}
```

**Output:**
```json
{
    "scenarios": [
        {
            "name": "Loan CRUD Lifecycle - Happy Path",
            "description": "Create, read, update, and verify a mortgage loan",
            "chain": {
                "steps": [
                    { "name": "Create", "method": "POST", "path": "/api/loans", "body": { "loanType": "MORTGAGE", "amount": 250000 }, "extract": { "loanId": "$.loanId" } },
                    { "name": "Read", "method": "GET", "path": "/api/loans/{{loanId}}", "assert": { "statusCode": 200 } },
                    { "name": "Update", "method": "PUT", "path": "/api/loans/{{loanId}}", "body": { "amount": 275000 }, "assert": { "statusCode": 200 } },
                    { "name": "Verify Update", "method": "GET", "path": "/api/loans/{{loanId}}", "assert": { "body.amount": 275000 } }
                ]
            }
        },
        {
            "name": "Loan Creation - Validation Error",
            "description": "Attempt to create a loan with missing required fields",
            "chain": {
                "steps": [
                    { "name": "Missing Amount", "method": "POST", "path": "/api/loans", "body": { "loanType": "MORTGAGE" }, "assert": { "statusCode": 400 } }
                ]
            }
        },
        {
            "name": "Loan Not Found",
            "description": "Request a non-existent loan",
            "chain": {
                "steps": [
                    { "name": "Get Missing", "method": "GET", "path": "/api/loans/NONEXISTENT", "assert": { "statusCode": 404 } }
                ]
            }
        }
    ],
    "totalScenarios": 3,
    "coverage": {
        "endpointsCovered": 3,
        "totalEndpoints": 5,
        "methodsCovered": ["GET", "POST", "PUT"],
        "statusCodesCovered": [200, 400, 404]
    }
}
```

**Generation strategies:**
- `crud-lifecycle`: Generate create/read/update/delete flows per resource
- `error-cases`: Focus on 4xx/5xx responses (invalid input, not found, unauthorized)
- `edge-cases`: Boundary values, empty arrays, null fields, large payloads
- `from-recordings`: Analyze existing recordings and generate variations
- `smoke`: One quick request per endpoint to verify availability

---

### 4.7 witness/suite-run

Execute a named test suite (a collection of chains/scenarios) in batch.

**Input:**
```json
{
    "suite": "regression-v2",
    "target": "https://staging.example.com",
    "options": {
        "parallel": 4,
        "stopOnFailure": false,
        "compareWith": "regression-v1-baseline"
    }
}
```

**Output:**
```json
{
    "suite": "regression-v2",
    "sessionId": "suite_regression-v2_20260208T1430",
    "totalScenarios": 15,
    "passed": 14,
    "failed": 1,
    "skipped": 0,
    "totalDurationMs": 12450,
    "failures": [
        {
            "scenario": "Loan Approval - Edge Case",
            "step": "Verify Interest Rate",
            "expected": { "body.interestRate": 6.75 },
            "actual": { "body.interestRate": 7.25 },
            "witnessId": "regression-v2_GET_api-loans-L001-rate_00000000_20260208T1432"
        }
    ],
    "comparisonSummary": {
        "comparedWith": "regression-v1-baseline",
        "totalComparisons": 14,
        "matches": 13,
        "differences": 1,
        "newEndpoints": 0,
        "removedEndpoints": 0
    }
}
```

---

### 4.8 witness/mock

Start a lightweight mock server that serves recorded responses. Useful for isolating a service from its dependencies during testing.

**Input:**
```json
{
    "sessionId": "regression-2026-02-08",
    "port": 8081,
    "options": {
        "matchBy": "method+path+bodyHash",
        "fallbackBehavior": "return-502",
        "latencySimulation": "recorded"
    }
}
```

**Output:**
```json
{
    "mockServerUrl": "http://localhost:8081",
    "endpointsMocked": 23,
    "endpoints": [
        { "method": "GET", "path": "/api/loans/*", "recordings": 8 },
        { "method": "POST", "path": "/api/loans", "recordings": 5 },
        { "method": "GET", "path": "/api/accounts/*", "recordings": 10 }
    ],
    "status": "running"
}
```

**Match strategies:**
- `method+path`: Match by HTTP method and path (ignoring body)
- `method+path+bodyHash`: Match by method, path, and request body hash (exact replay)
- `method+path+partial`: Match by method and path prefix (wildcard)

**Latency simulation:**
- `none`: Respond immediately
- `recorded`: Replay the original response latency
- `fixed`: Add fixed delay (configurable)
- `random`: Add random jitter within a range

---

### 4.9 witness/list

Browse recorded sessions and interactions.

**Input:**
```json
{
    "filter": {
        "sessionId": "regression-*",
        "tags": ["mortgage"],
        "dateRange": {
            "from": "2026-02-01",
            "to": "2026-02-08"
        },
        "statusCode": 500
    },
    "limit": 20,
    "offset": 0
}
```

**Output:**
```json
{
    "total": 3,
    "interactions": [
        {
            "witnessId": "regression_POST_api-loans_a1b2c3d4_20260205T0930",
            "sessionId": "regression-2026-02-05",
            "method": "POST",
            "path": "/api/loans",
            "statusCode": 500,
            "durationMs": 1234,
            "tags": ["mortgage", "error-case"],
            "timestamp": "2026-02-05T09:30:00Z"
        }
    ]
}
```

---

### 4.10 witness/inspect

View the full details of a recorded interaction.

**Input:**
```json
{
    "witnessId": "regression_POST_api-loans_a1b2c3d4_20260205T0930"
}
```

**Output:** Returns the complete `Interaction` record (see Section 3.1), including request, response, headers, outbound calls, and metadata.

---

## 5. Key Design Decisions

### 5.1 Client-Side vs. Middleware

The original library was a middleware — it sat inside the target API and intercepted requests. This worked for testing your own APIs but couldn't test external services.

The redesign is **client-side** — the MCP server drives requests from outside, acting as an HTTP client. This means it can test any API, anywhere, without requiring middleware installation. The trade-off is that outbound call capture requires a proxy setup rather than automatic middleware interception.

For scenarios where middleware-level capture is valuable (e.g., capturing internal dependency calls), the redesign includes an optional companion middleware package that can be installed in .NET services:

```csharp
// Optional: Install in your service for outbound call capture
app.UseWitnessCapture(options =>
{
    options.ExportEndpoint = "http://witness-mcp-server:9090/ingest";
    options.CaptureOutbound = true;
});
```

### 5.2 Storage Strategy

**Default: Local filesystem** — Zero setup, works everywhere. JSON files organized by session.

**Optional: Azure Blob Storage** — For shared team access and CI/CD integration. Same layout as local, just in blob containers. Preserves backward compatibility with the original library storage format.

**Optional: SQLite index** — For fast search and filtering across large recording sets. The JSON files remain the source of truth; SQLite is a read-through index.

### 5.3 Authentication

The MCP server does not manage authentication itself. Agents pass auth headers (Bearer tokens, API keys, etc.) in the `headers` field of each request. For convenience, sessions can store reusable auth configs:

```json
{
    "sessionAuth": {
        "type": "bearer",
        "tokenEndpoint": "https://auth.example.com/token",
        "clientId": "test-client",
        "clientSecret": "{{env:CLIENT_SECRET}}",
        "scope": "api.read api.write"
    }
}
```

The `{{env:VARIABLE}}` syntax pulls from environment variables, avoiding secrets in stored files.

### 5.4 Idempotency and Determinism

Recording the same request twice produces the same WitnessId (deterministic hashing). This enables deduplication and makes it safe to re-record without polluting the store.

For non-deterministic responses (timestamps, UUIDs), the `witness/compare` tool's `ignoreFields` and `numericTolerance` options handle the diff gracefully.

---

## 6. Integration Patterns

### 6.1 CI/CD Regression Testing

```
1. Developer pushes code
2. CI pipeline starts
3. Agent calls witness/suite-run with baseline suite
4. Agent calls witness/compare for each interaction vs. baseline
5. Pipeline fails if any comparison shows error-severity differences
6. New recordings become the baseline for next run
```

### 6.2 API Migration Validation

```
1. Record full interaction set against legacy API (source of truth)
2. Replay every recording against modernized API
3. Compare all responses
4. Generate migration report: what matches, what differs, what's missing
```

### 6.3 Contract Testing

```
1. witness/discover parses provider's OpenAPI spec
2. witness/generate creates scenarios from spec
3. witness/chain executes scenarios
4. If provider changes spec, re-run and compare with previous recordings
5. Breaking changes detected automatically
```

### 6.4 Development Mocking

```
1. Record interactions with a real third-party API
2. witness/mock serves recorded responses on localhost
3. Develop against mock (fast, no API costs, offline-capable)
4. Before release, re-record against real API and compare
```

---

## 7. Configuration

```json
{
    "witness": {
        "storage": {
            "type": "local",
            "path": "./witness-store",
            "blobConnectionString": null,
            "blobContainerName": "witness",
            "enableSqliteIndex": false
        },
        "defaults": {
            "timeoutMs": 30000,
            "followRedirects": true,
            "validateSchema": false,
            "captureResponseHeaders": true
        },
        "mock": {
            "defaultPort": 8081,
            "defaultMatchStrategy": "method+path+bodyHash",
            "defaultLatencySimulation": "none"
        },
        "comparison": {
            "defaultIgnoreFields": ["timestamp", "requestId", "date", "x-request-id"],
            "defaultNumericTolerance": 0.001,
            "defaultArrayOrderMatters": false
        },
        "server": {
            "port": 9090,
            "transport": "stdio"
        }
    }
}
```

---

## 8. Implementation Plan

### Phase 1: Core (Weeks 1–3)

- [ ] MCP server scaffold (Node.js or .NET)
- [ ] `witness/record` — HTTP execution + JSON storage
- [ ] `witness/replay` — Load and re-execute
- [ ] `witness/inspect` and `witness/list`
- [ ] WitnessId generation
- [ ] Local filesystem storage

### Phase 2: Comparison (Weeks 4–5)

- [ ] `witness/compare` — JSON diff engine with tolerances
- [ ] Severity classification (error/warning/info)
- [ ] Performance delta tracking

### Phase 3: Flows (Weeks 6–8)

- [ ] `witness/chain` — Multi-step execution with variable extraction
- [ ] `witness/discover` — OpenAPI spec parser
- [ ] Schema validation against OpenAPI
- [ ] `witness/generate` — AI scenario generation

### Phase 4: Suites & Mocking (Weeks 9–11)

- [ ] `witness/suite-run` — Batch execution with parallel support
- [ ] `witness/mock` — Mock server from recordings
- [ ] Suite definition format and persistence

### Phase 5: Storage & Polish (Weeks 12–13)

- [ ] Azure Blob Storage adapter
- [ ] SQLite index for search
- [ ] Optional .NET middleware companion package
- [ ] Documentation, examples, npm/NuGet publish

---

## 9. Legacy Displacement & Modernization Patterns

This section describes how Witness MCP Server applies to legacy system modernization scenarios. While the tool itself is domain-agnostic, the record/replay/compare model maps naturally to established displacement patterns where behavioral equivalence between old and new systems must be proven.

### 9.1 Strangler Fig Verification

The Strangler Fig pattern incrementally replaces legacy functionality by routing requests to either the old or new system. Each endpoint cutover is a high-risk moment. Witness provides the evidence framework for each cutover step.

#### Before Cutover: Baseline Capture

Record a comprehensive interaction set against the legacy service. This becomes the behavioral contract — the ground truth of what the old system does.

```json
{
    "steps": [
        { "name": "Record baseline", "tool": "witness/suite-run",
          "suite": "account-management-baseline",
          "target": "https://legacy-service/api" }
    ]
}
```

#### During Cutover: Shadow Validation

Run both systems in parallel. For every request, record against both targets and compare responses. This is dual-write validation without building custom infrastructure.

```json
{
    "steps": [
        {
            "name": "Execute against legacy",
            "tool": "witness/record",
            "target": "https://legacy-service",
            "method": "POST", "path": "/api/accounts",
            "body": { "customerId": "C001", "type": "SAVINGS" },
            "options": { "tag": "shadow-legacy", "sessionId": "cutover-wave-1" }
        },
        {
            "name": "Execute same request against modern",
            "tool": "witness/record",
            "target": "https://modern-service",
            "method": "POST", "path": "/api/accounts",
            "body": { "customerId": "C001", "type": "SAVINGS" },
            "options": { "tag": "shadow-modern", "sessionId": "cutover-wave-1" }
        },
        {
            "name": "Compare",
            "tool": "witness/compare",
            "sourceWitnessId": "shadow-legacy_POST_api-accounts_*",
            "targetWitnessId": "shadow-modern_POST_api-accounts_*",
            "options": { "ignoreFields": ["timestamp", "requestId"] }
        }
    ]
}
```

#### After Cutover: Canary Monitoring

Continue recording against the new service in production. If anomalies appear, replay the failing request against the legacy service (if still available) to determine whether it's a regression or a pre-existing behavior.

#### Cutover Evidence Package

Each endpoint cutover produces a witness session that serves as the migration evidence:

```
Wave 1: Account Management
  └── session: "wave-1-baseline"    (recorded against legacy)
  └── session: "wave-1-modern"      (replayed against new service)
  └── comparison: "wave-1-parity"   (100% match → go live)

Wave 2: Loan Processing
  └── session: "wave-2-baseline"
  └── session: "wave-2-modern"
  └── comparison: "wave-2-parity"   (3 differences → investigate)

Wave 3: Payment Processing
  └── session: "wave-3-baseline"    (recordings ready, cutover pending)
```

The comparison report for each wave becomes the **go/no-go evidence** for the cutover decision. Stakeholders see structured proof showing exactly what was validated, not just "testing passed."

---

### 9.2 DDD Bubble Context Validation

A Bubble Context is a clean new domain model that coexists with the legacy system, connected through an Anti-Corruption Layer. Witness validates three critical aspects of the bubble.

#### Bubble External Contract Stability

The bubble exposes new REST APIs representing the clean domain model. As you evolve the bubble's internal implementation (refactoring aggregates, changing event handling, optimizing queries), the external behavior must remain stable.

```json
{
    "suite": "bubble-lending-contracts",
    "scenarios": [
        {
            "name": "Create Loan - Clean Model",
            "chain": {
                "steps": [
                    {
                        "name": "Create via bubble API",
                        "method": "POST",
                        "path": "/api/v2/loans",
                        "body": {
                            "borrower": { "name": "Alice", "creditScore": 750 },
                            "property": { "value": 400000, "type": "residential" },
                            "requestedAmount": 320000
                        },
                        "extract": { "loanId": "$.loanId" },
                        "assert": { "statusCode": 201 }
                    },
                    {
                        "name": "Read back via bubble API",
                        "method": "GET",
                        "path": "/api/v2/loans/{{loanId}}",
                        "assert": {
                            "body.borrower.name": "Alice",
                            "body.status": "PENDING_REVIEW",
                            "body.requestedAmount": 320000
                        }
                    }
                ]
            }
        }
    ]
}
```

Record this suite once. Replay after every internal refactoring. The bubble's contract is proven stable.

#### Bubble-to-Legacy Translation Accuracy

The ACL translates between the bubble's clean model and the legacy system's structure. This translation is the highest-risk code in the architecture.

```json
{
    "name": "ACL Translation - Loan Creation",
    "chain": {
        "steps": [
            {
                "name": "Submit via bubble (clean model)",
                "method": "POST",
                "path": "/api/v2/loans",
                "body": {
                    "borrower": { "name": "Alice", "creditScore": 750 },
                    "requestedAmount": 320000
                },
                "extract": { "loanId": "$.loanId" }
            },
            {
                "name": "Verify legacy received correct translation",
                "method": "GET",
                "path": "/legacy/api/LOAN-INQUIRY",
                "body": { "LOAN-ID": "{{loanId}}" },
                "assert": {
                    "body.CUST-NAME": "ALICE",
                    "body.CUST-CRED-SCORE": "750",
                    "body.LOAN-AMT": "320000.00",
                    "body.LOAN-STAT": "PR"
                }
            }
        ]
    }
}
```

This chain proves the ACL correctly translates between models: `borrower.name` → `CUST-NAME`, `"PENDING_REVIEW"` → `"PR"`, decimal formatting preserved. If someone modifies the ACL mapping, this chain breaks immediately.

#### Bubble Expansion Verification

As you grow the bubble (migrating more functionality from legacy into the clean model), each expansion is a mini-cutover. Record the interactions that currently go through the legacy path, implement them in the bubble, replay, compare. The bubble grows one verified capability at a time.

```
Bubble v1: Account Inquiry only
  └── suite: "bubble-v1" (5 scenarios, all pass)

Bubble v2: Account Inquiry + Loan Creation
  └── suite: "bubble-v2" (12 scenarios)
  └── includes: all bubble-v1 scenarios (regression)
  └── adds: 7 new loan creation scenarios
  └── comparison: bubble-v1 scenarios still pass identically
```

---

### 9.3 Anti-Corruption Layer Testing

The ACL is typically the most fragile component in a modernization architecture. It has two directions, both testable with Witness.

#### Inbound ACL (Legacy → Modern)

The legacy system sends requests in the old format. The ACL translates them into the clean domain model before forwarding to the modern service.

```
Legacy System → ACL → Modern Service

Chain:
  Step 1: POST /acl/inbound (legacy-format flat record)
          body: { "CUST-ID": "C001", "ACCT-TYP": "SAV", "INIT-DEP": "5000.00" }
  Step 2: GET /modern/api/accounts?customerId=C001 (verify clean model)
          assert: { "body.type": "SAVINGS", "body.initialDeposit": 5000.00 }
```

#### Outbound ACL (Modern → Legacy)

The modern service needs data from the legacy system. The ACL translates the clean-model request into the legacy format and translates the response back.

```
Modern Service → ACL → Legacy System

Chain:
  Step 1: POST /api/v2/loans/L001/credit-check (clean model request)
  Step 2: Verify ACL translated to legacy format
          GET /acl/outbound-log/last
          assert: { "body.legacyRequest.CRED-CHK-CUST": "C001" }
  Step 3: Verify response translated back to clean model
          assert from step 1: { "body.creditScore": 750, "body.riskCategory": "LOW" }
```

#### ACL Regression After Legacy Patches

The legacy system receives a maintenance patch (it's still running during modernization). Record all ACL interactions before the patch, replay after. Any differences mean the ACL mapping needs updating.

#### ACL Elimination Validation

The end goal is removing the ACL entirely. When the modern service can handle everything directly, replay the full ACL test suite against the modern service without the ACL in the path. If everything matches, the ACL is ready for decommission.

```
Phase 1: Record through ACL path    → suite "acl-baseline"
Phase 2: Replay directly to modern   → suite "acl-bypass"
Phase 3: Compare                     → 0 differences = ACL can be removed
```

---

### 9.4 Event-Driven Transition Patterns

When migrating from synchronous calls to event-driven architecture, the communication model changes fundamentally but the observable business outcomes must remain identical.

#### Synchronous-to-Async Equivalence

The legacy system performs a synchronous CALL to update an account balance. The modern system publishes a `BalanceUpdated` event that is processed asynchronously. Different mechanism, same result.

```json
{
    "name": "Balance Update - Sync vs Async Equivalence",
    "chain": {
        "steps": [
            {
                "name": "Get state BEFORE",
                "method": "GET",
                "path": "/api/accounts/A001",
                "extract": { "balanceBefore": "$.balance" }
            },
            {
                "name": "Trigger update (modern async path)",
                "method": "POST",
                "path": "/api/accounts/A001/deposit",
                "body": { "amount": 500.00, "reference": "DEP-001" },
                "assert": { "statusCode": 202 }
            },
            {
                "name": "Wait for event processing",
                "method": "GET",
                "path": "/api/accounts/A001",
                "options": {
                    "retryUntil": { "body.balance": 5500.00 },
                    "maxRetries": 10,
                    "retryDelayMs": 500
                },
                "extract": { "balanceAfter": "$.balance" }
            },
            {
                "name": "Verify final state matches legacy behavior",
                "assert": {
                    "balanceAfter": 5500.00
                }
            }
        ]
    }
}
```

Record this chain against both the legacy synchronous path and the modern async path. Compare the final state assertions. The intermediate mechanism changed but the business outcome is identical.

#### Saga Validation

Complex business processes that were ACID transactions in the legacy system become sagas in the modern system. Record the legacy transaction's input and final state. Replay the same business operation through the saga and verify the end state matches.

```json
{
    "name": "Loan Approval Saga - Happy Path",
    "chain": {
        "steps": [
            { "name": "Submit application", "method": "POST", "path": "/api/loans",
              "body": { "amount": 250000, "type": "MORTGAGE" },
              "extract": { "loanId": "$.loanId" } },

            { "name": "Wait for credit check (saga step 1)",
              "method": "GET", "path": "/api/loans/{{loanId}}",
              "options": { "retryUntil": { "body.creditCheckComplete": true }, "maxRetries": 20 } },

            { "name": "Wait for appraisal (saga step 2)",
              "method": "GET", "path": "/api/loans/{{loanId}}",
              "options": { "retryUntil": { "body.appraisalComplete": true }, "maxRetries": 20 } },

            { "name": "Verify final state",
              "method": "GET", "path": "/api/loans/{{loanId}}",
              "assert": {
                  "body.status": "APPROVED",
                  "body.creditCheckComplete": true,
                  "body.appraisalComplete": true
              }
            }
        ]
    }
}
```

For compensation testing, trigger a failure mid-saga and verify the system rolls back correctly:

```json
{
    "name": "Loan Approval Saga - Compensation on Appraisal Failure",
    "chain": {
        "steps": [
            { "name": "Submit application", "method": "POST", "path": "/api/loans",
              "body": { "amount": 5000000, "type": "MORTGAGE" },
              "extract": { "loanId": "$.loanId" } },

            { "name": "Wait for credit check pass", "method": "GET",
              "path": "/api/loans/{{loanId}}",
              "options": { "retryUntil": { "body.creditCheckComplete": true }, "maxRetries": 20 } },

            { "name": "Wait for appraisal rejection + compensation", "method": "GET",
              "path": "/api/loans/{{loanId}}",
              "options": { "retryUntil": { "body.status": "REJECTED" }, "maxRetries": 30 } },

            { "name": "Verify compensation occurred",
              "method": "GET", "path": "/api/loans/{{loanId}}",
              "assert": {
                  "body.status": "REJECTED",
                  "body.rejectionReason": "APPRAISAL_VALUE_INSUFFICIENT",
                  "body.creditHoldReleased": true
              }
            }
        ]
    }
}
```

---

### 9.5 Domain Event Discovery Confirmation

When AI agents infer domain events from legacy code patterns (write-then-call, status field changes), Witness can confirm these inferences at runtime.

```json
{
    "name": "Confirm Inferred Event: AccountClosed",
    "description": "BCIE inferred AccountClosed from UPDATE ACCOUNT SET STATUS='CLOSED'. Verify at runtime.",
    "chain": {
        "steps": [
            {
                "name": "Capture state BEFORE",
                "method": "GET",
                "path": "/api/accounts/A001",
                "extract": { "statusBefore": "$.status" },
                "assert": { "body.status": "ACTIVE" }
            },
            {
                "name": "Trigger the close operation",
                "method": "POST",
                "path": "/api/accounts/A001/close",
                "body": { "reason": "customer-request" },
                "assert": { "statusCode": 200 }
            },
            {
                "name": "Capture state AFTER",
                "method": "GET",
                "path": "/api/accounts/A001",
                "assert": { "body.status": "CLOSED" }
            },
            {
                "name": "Verify event was published",
                "method": "GET",
                "path": "/api/events?entity=Account&entityId=A001&type=AccountClosed",
                "assert": {
                    "statusCode": 200,
                    "body.events[0].type": "AccountClosed",
                    "body.events[0].payload.previousStatus": "ACTIVE",
                    "body.events[0].payload.newStatus": "CLOSED",
                    "body.events[0].payload.reason": "customer-request"
                }
            }
        ]
    }
}
```

When this chain passes, the inferred event is marked as `confirmed: true` in the knowledge graph. When it fails, either the inference was wrong (no event actually occurs) or the implementation is incomplete.

---

### 9.6 Bounded Context Independence Validation

A bounded context should be independently deployable. Witness validates this by testing each context in isolation.

#### Context Isolation Test

For each bounded context, record its full API interaction suite with all other contexts replaced by mocks:

```
Step 1: witness/mock — serve recorded responses for all dependency contexts
Step 2: witness/suite-run — run the context's test suite against the real service
Step 3: If suite passes with mocked dependencies → context is genuinely independent
        If suite fails → hidden coupling exists
```

#### Cross-Context Contract Testing

Record Context A's outbound calls to Context B. These recordings define the contract between them. When Context B deploys a new version independently, replay Context A's recordings against it.

```
Context A records calls to Context B → "a-to-b-contract" session
Context B deploys v2
Replay "a-to-b-contract" against Context B v2
Compare with baseline
Contract maintained? → Ship independently.
Contract broken? → Coordinate deployment or update ACL.
```

#### Context Boundary Visibility

Record an end-to-end business process that spans multiple contexts. The chain structure makes context transitions visible:

```json
{
    "name": "Loan Origination - Cross-Context Flow",
    "chain": {
        "steps": [
            { "name": "[Customer Context] Create customer profile",
              "method": "POST", "path": "/customer-api/customers",
              "extract": { "customerId": "$.id" } },

            { "name": "[Lending Context] Create loan application",
              "method": "POST", "path": "/lending-api/loans",
              "body": { "customerId": "{{customerId}}", "amount": 250000 } ,
              "extract": { "loanId": "$.loanId" } },

            { "name": "[Risk Context] Request credit assessment",
              "method": "POST", "path": "/risk-api/assessments",
              "body": { "customerId": "{{customerId}}", "loanId": "{{loanId}}" },
              "extract": { "assessmentId": "$.assessmentId" } },

            { "name": "[Lending Context] Approve with assessment",
              "method": "POST", "path": "/lending-api/loans/{{loanId}}/approve",
              "body": { "assessmentId": "{{assessmentId}}" } },

            { "name": "[Notification Context] Verify notification sent",
              "method": "GET", "path": "/notification-api/notifications?ref={{loanId}}",
              "assert": { "body.notifications[0].type": "LOAN_APPROVED" } }
        ]
    }
}
```

Each step explicitly names the context it targets. If a step secretly calls a different context's database directly (instead of going through the API), the chain wouldn't capture it — which is itself a signal that the boundary isn't clean.

---

### 9.7 Migration Wave Evidence Framework

Modernization is typically delivered in waves, each cutting over a set of capabilities. Witness provides structured evidence for every wave.

#### Wave Lifecycle

```
┌─────────────────────────────────────────────────────────────────┐
│ Wave N Lifecycle                                                 │
│                                                                  │
│  1. BASELINE                                                     │
│     witness/suite-run against legacy                            │
│     → session: "wave-N-baseline"                                 │
│                                                                  │
│  2. DEVELOPMENT                                                  │
│     witness/mock legacy dependencies                            │
│     witness/suite-run against modern (WIP)                      │
│     → incremental comparison: track parity progress              │
│                                                                  │
│  3. VALIDATION                                                   │
│     witness/replay baseline against modern                      │
│     witness/compare all interactions                            │
│     → comparison: "wave-N-parity-report"                         │
│                                                                  │
│  4. GO/NO-GO                                                     │
│     Parity report reviewed by architects + business              │
│     Accepted differences documented as ADRs                      │
│     → decision: cutover or iterate                               │
│                                                                  │
│  5. POST-CUTOVER                                                 │
│     witness/suite-run as production canary                      │
│     → ongoing: "wave-N-production-monitoring"                    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

#### Parity Progress Dashboard

During development, track parity progress over time by running comparisons daily:

```
Week 1: 247 / 847 interactions match  (29%)
Week 2: 512 / 847 interactions match  (60%)
Week 3: 789 / 847 interactions match  (93%)
Week 4: 841 / 847 interactions match  (99.3%)
Week 4: 6 remaining differences accepted as intentional improvements
        → documented in ADRs
        → GO decision
```

Each daily run is a `witness/suite-run` + `witness/compare` that produces a structured report. The trajectory from 29% to 99.3% is visible, measurable evidence of modernization progress — far more meaningful to stakeholders than "testing is going well."

#### Cross-Wave Regression

After Wave 2 is deployed, re-run Wave 1's suite to ensure nothing regressed:

```json
{
    "pipeline": [
        { "tool": "witness/suite-run", "suite": "wave-1-regression", "target": "https://production" },
        { "tool": "witness/suite-run", "suite": "wave-2-regression", "target": "https://production" },
        { "tool": "witness/compare", "source": "wave-1-regression-baseline", "target": "wave-1-regression-current" },
        { "tool": "witness/compare", "source": "wave-2-regression-baseline", "target": "wave-2-regression-current" }
    ]
}
```

Every wave carries forward the accumulated regression suites of all previous waves. The test estate grows with each wave, providing increasing confidence that the overall system remains correct.

---

## 10. Scenario Catalog

Beyond modernization, Witness MCP Server supports a broad range of use cases. The following catalog organizes them by role.

### 10.1 Developer Scenarios

| Scenario | How Witness Helps |
|----------|--------------------|
| **Pre-commit regression** | Record before code change, replay after, compare. Instant regression check. |
| **Offline development** | Record third-party API interactions, `witness/mock` serves them locally. No network, no API costs, no rate limits. |
| **API exploration** | `witness/discover` + `witness/generate` to systematically explore an unfamiliar API. Recordings become living documentation. |
| **Debug reproduction** | Record the failing production request, share WitnessId with colleague. They `witness/inspect` to see exactly what happened. |

### 10.2 QA & Testing Scenarios

| Scenario | How Witness Helps |
|----------|--------------------|
| **Contract testing** | Team A records their API usage. Team B replays recordings after each deployment. Breaking changes surface automatically. |
| **Multi-environment validation** | Same suite runs against dev, staging, pre-prod. Compare responses to catch environment-specific issues. |
| **Chaos testing** | `witness/mock` with injected faults: 500s, timeouts, malformed JSON. Verify client resilience. |
| **Load test scenario capture** | Record real user flows, export as load test input for k6/JMeter/NBomber. |

### 10.3 Security Scenarios

| Scenario | How Witness Helps |
|----------|--------------------|
| **Penetration test replay** | Record pentest interactions, apply fixes, replay to verify vulnerabilities are closed. |
| **Credential rotation** | Record interactions, rotate secrets, replay with new credentials. Verify old credentials rejected. |
| **Audit trail** | Every recording is timestamped, structured evidence of request/response. Compliance artifact. |

### 10.4 AI Application Scenarios

| Scenario | How Witness Helps |
|----------|--------------------|
| **Prompt regression** | Record LLM API responses for test prompts. After changing system prompt or switching models, replay and compare quality. |
| **Agent workflow validation** | Record an agent's full tool-call sequence. After updating instructions, replay and compare outcomes. |
| **Cost estimation** | Record representative LLM calls with token counts. Replay against cheaper model. Compare quality vs. cost. |

### 10.5 Architecture Scenarios

| Scenario | How Witness Helps |
|----------|--------------------|
| **Database migration** | Record API behavior with Postgres, switch to CosmosDB, replay, compare. API responses should be identical. |
| **Service mesh refactoring** | Record service-to-service calls at boundaries. Refactor internal service, replay at boundaries, compare. |
| **Version compatibility** | Record against API v1, replay against v2 and v3. Comparison reports become the compatibility matrix. |
| **Deprecation impact** | Search recordings to see which flows use the endpoint being deprecated. No hits = safe to remove. |

---

## 11. Future Directions

- **Witness Studio**: VS Code extension for browsing recordings, comparing diffs visually, and managing suites
- **Traffic capture mode**: Proxy mode that records all traffic passing through (like a recording reverse proxy)
- **GraphQL support**: Extend beyond REST to GraphQL queries/mutations
- **gRPC support**: Protocol buffer recording and replay
- **Performance benchmarking**: Statistical comparison across multiple runs (p50, p95, p99)
- **Webhook recording**: Capture and replay async webhook callbacks
- **OpenTelemetry correlation**: Link recordings to distributed traces for full observability
- **Knowledge graph integration**: Export interaction data as graph nodes for architectural analysis tools
- **Parity progress tracking**: Time-series dashboard showing match rate convergence during migrations

---

*— End of Specification —*
