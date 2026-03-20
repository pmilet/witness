# Witness MCP Server - Usage Examples

This document provides examples of how to use the Witness MCP Server tools.

## Prerequisites

Make sure you have:
1. Installed the Witness MCP Server: `npm install -g @pmilet/witness-mcp`
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
