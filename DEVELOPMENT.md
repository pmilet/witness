# Witness MCP Server - Development Guide

## Project Structure

```
witness/
├── src/
│   ├── index.ts              # Main MCP server entry point
│   ├── types/
│   │   └── index.ts          # TypeScript interfaces and types
│   ├── core/
│   │   ├── witnessId.ts      # WitnessId generation logic
│   │   └── httpExecutor.ts   # HTTP request execution
│   ├── storage/
│   │   └── interactionStore.ts # Local filesystem storage
│   └── tools/
│       └── index.ts          # MCP tool implementations
├── dist/                     # Compiled JavaScript (generated)
├── examples/                 # Usage examples
├── package.json
├── tsconfig.json
└── witness.config.json       # Configuration example

```

## Development Setup

### Prerequisites
- Node.js 18 or higher
- npm or yarn

### Installation

1. Clone the repository:
```bash
git clone https://github.com/pmilet/witness.git
cd witness
```

2. Install dependencies:
```bash
npm install
```

3. Build the project:
```bash
npm run build
```

### Development Workflow

**Watch mode** (automatically rebuilds on changes):
```bash
npm run dev
```

**Build**:
```bash
npm run build
```

**Test locally**:
```bash
node dist/index.js
```

## Architecture

### Core Components

1. **HttpExecutor** (`src/core/httpExecutor.ts`)
   - Executes HTTP requests using axios
   - Measures response time
   - Handles errors gracefully
   - Supports all HTTP methods

2. **WitnessId Generator** (`src/core/witnessId.ts`)
   - Creates deterministic, human-readable IDs
   - Uses SHA-256 for body hashing
   - Truncates path slugs to 60 characters
   - Format: `{tag}_{method}_{path-slug}_{body-hash}_{timestamp}`

3. **InteractionStore** (`src/storage/interactionStore.ts`)
   - Manages local filesystem storage
   - Organizes data by sessions
   - Maintains session metadata
   - Supports listing and searching

4. **MCP Tools** (`src/tools/index.ts`)
   - witness/record - Execute and capture interactions
   - witness/replay - Replay recorded interactions
   - witness/inspect - View interaction details
   - witness/list - Browse sessions and interactions

### Data Model

**Interaction** - Core data structure for recorded HTTP interactions:
```typescript
{
  witnessId: string;
  sessionId: string;
  timestamp: string;
  request: {
    method: string;
    url: string;
    path: string;
    headers: Record<string, string>;
    body?: any;
    contentType?: string;
  };
  response: {
    statusCode: number;
    headers: Record<string, string>;
    body?: any;
    contentType?: string;
    durationMs: number;
  };
  metadata: {
    tags: string[];
    description?: string;
    openApiOperationId?: string;
    chainStep?: number;
    chainId?: string;
  };
}
```

## Testing

### Manual Testing with MCP Inspector

1. Install MCP Inspector:
```bash
npx @modelcontextprotocol/inspector dist/index.js
```

2. This will open a web interface where you can:
   - Test tool calls
   - Inspect request/response
   - Debug issues

### Integration Testing

You can test the tools manually by configuring them in an MCP client:

**Claude Desktop** (`~/Library/Application Support/Claude/claude_desktop_config.json`):
```json
{
  "mcpServers": {
    "witness": {
      "command": "node",
      "args": ["/path/to/witness/dist/index.js"]
    }
  }
}
```

**VS Code (GitHub Copilot)** (`.vscode/mcp.json`):
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

### Test Against Real APIs

Use free test APIs for development:
- JSONPlaceholder: https://jsonplaceholder.typicode.com
- ReqRes: https://reqres.in/api
- httpbin: https://httpbin.org

## Contributing

### Code Style

- Use TypeScript strict mode
- Follow existing naming conventions
- Add JSDoc comments for public APIs
- Keep functions focused and small

### Adding New Tools

1. Define the tool in `src/index.ts` TOOLS array
2. Implement the handler in `src/tools/index.ts`
3. Update documentation in `examples/USAGE.md`

Example:
```typescript
// In src/index.ts
{
  name: 'witness/new-tool',
  description: 'Description of what the tool does',
  inputSchema: {
    type: 'object',
    properties: {
      param1: { type: 'string', description: '...' }
    },
    required: ['param1']
  }
}

// In src/tools/index.ts
export async function newTool(args: any, context: ToolContext) {
  // Implementation
}

// In src/index.ts CallToolRequestSchema handler
case 'witness/new-tool':
  return await newTool(args, context);
```

## Roadmap

### Phase 1 (Current - v0.1.0) ✓
- [x] Core infrastructure
- [x] witness/record
- [x] witness/replay
- [x] witness/inspect
- [x] witness/list

### Phase 2 (v0.2.0)
- [ ] witness/compare with diff engine
- [ ] Configurable comparison tolerances
- [ ] Performance tracking

### Phase 3 (v0.3.0)
- [ ] witness/chain for multi-step flows
- [ ] Variable extraction and substitution
- [ ] Step assertions

### Phase 4 (v0.4.0)
- [ ] witness/discover (OpenAPI parsing)
- [ ] witness/generate (AI scenario generation)

### Phase 5 (v0.5.0)
- [ ] witness/suite-run (batch execution)
- [ ] witness/mock (mock server)

## Troubleshooting

### "Module not found" errors
Make sure you've run `npm run build` after making changes.

### MCP server won't start
Check stderr output: `node dist/index.js 2>&1`

### Storage issues
Delete `witness-store/` and restart to reset storage.

## License

Apache-2.0 - See LICENSE file for details.
