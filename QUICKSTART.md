# Witness MCP Server - Quick Start Guide

## Installation

### From Source

1. **Clone the repository:**
```bash
git clone https://github.com/pmilet/witness.git
cd witness
```

2. **Install dependencies:**
```bash
npm install
```

3. **Build the project:**
```bash
npm run build
```

4. **Test the installation:**
```bash
npm test
```

### Using npx (Future)

Once published to npm:
```bash
npx @pmilet/witness-mcp
```

## Configuration

### For Claude Desktop

Add to `~/Library/Application Support/Claude/claude_desktop_config.json` (macOS) or `%APPDATA%\Claude\claude_desktop_config.json` (Windows):

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

### For VS Code with GitHub Copilot

Create `.vscode/mcp.json` in your workspace:

```json
{
  "mcp": {
    "servers": {
      "witness": {
        "command": "node",
        "args": ["./dist/index.js"]
      }
    }
  }
}
```

### For Other MCP Clients

Use the standard MCP server configuration with:
- **Command:** `node`
- **Args:** `["/path/to/witness/dist/index.js"]`
- **Transport:** stdio

## First Steps

After configuring your MCP client, try these commands:

### 1. List Available Tools

Ask your AI agent:
> "What tools are available from the witness server?"

You should see:
- witness/record
- witness/replay
- witness/inspect
- witness/list

### 2. Record Your First Interaction

Ask your AI agent:
> "Use witness/record to call GET https://api.example.com/users/1"

This will:
- Execute the HTTP request
- Capture the response
- Store the interaction
- Return a WitnessId

### 3. List Recorded Sessions

> "Use witness/list to show all recorded sessions"

### 4. Inspect an Interaction

> "Use witness/inspect to view the details of WitnessId: {the-id-from-step-2}"

### 5. Replay an Interaction

> "Use witness/replay to replay that same request against https://api-v2.example.com"

## Configuration Options

Create `witness.config.json` in your working directory:

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
    }
  }
}
```

## Common Use Cases

### API Testing

Record interactions as you develop, then replay them after changes to verify behavior hasn't changed.

### API Migration

Record all interactions with the old API, then replay them against the new API to verify compatibility.

### Environment Validation

Record interactions in one environment (dev), replay in another (staging, production) to ensure consistency.

### Offline Development

Record interactions with third-party APIs, then work offline using the recorded responses.

## Storage

By default, all interactions are stored in `./witness-store/`:

```
witness-store/
└── sessions/
    └── {sessionId}/
        ├── session.json
        └── interactions/
            └── {witnessId}.json
```

You can browse these files directly if needed, though the `witness/list` and `witness/inspect` tools provide a better interface.

## Troubleshooting

### Server won't start

1. Check that you've built the project: `npm run build`
2. Verify the path in your MCP client configuration
3. Check stderr output for error messages

### Tool calls fail

1. Check network connectivity
2. Verify the target URL is accessible
3. Look at the error message in the response

### Storage issues

1. Ensure you have write permissions in the current directory
2. Check available disk space
3. Try deleting `./witness-store/` to reset

## Next Steps

- Read the [full specification](witness-mcp-server-spec.md) for details
- See [usage examples](examples/USAGE.md) for more scenarios
- Check [development guide](DEVELOPMENT.md) if you want to contribute

## Support

- Issues: https://github.com/pmilet/witness/issues
- Discussions: https://github.com/pmilet/witness/discussions
