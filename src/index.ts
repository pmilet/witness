#!/usr/bin/env node
/**
 * Witness MCP Server
 * Main entry point for the Model Context Protocol server
 */

import { Server } from '@modelcontextprotocol/sdk/server/index.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import {
  CallToolRequestSchema,
  ListToolsRequestSchema,
} from '@modelcontextprotocol/sdk/types.js';
import { promises as fs } from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';

import { HttpExecutor } from './core/httpExecutor.js';
import { InteractionStore } from './storage/interactionStore.js';
import { recordTool, replayTool, inspectTool, listTool, compareTool, ToolContext } from './tools/index.js';
import { WitnessConfig } from './types/index.js';

/**
 * Load witness.config.json from the directory containing this script,
 * or the current working directory, falling back to built-in defaults.
 */
async function loadConfig(): Promise<WitnessConfig> {
  const defaults: WitnessConfig = {
    storage: { type: 'local', path: './witness-store' },
    defaults: { timeoutMs: 30000, followRedirects: true },
    comparison: { defaultIgnoreFields: ['timestamp', 'requestId', 'date'], defaultNumericTolerance: 0.001 }
  };

  const searchDirs = [
    path.dirname(fileURLToPath(import.meta.url)),
    process.cwd()
  ];

  for (const dir of searchDirs) {
    const configPath = path.join(dir, 'witness.config.json');
    try {
      const raw = await fs.readFile(configPath, 'utf-8');
      const parsed = JSON.parse(raw) as { witness?: WitnessConfig };
      if (parsed.witness) {
        // Deep-merge parsed config over defaults
        return {
          storage: { ...defaults.storage, ...parsed.witness.storage },
          defaults: { ...defaults.defaults, ...parsed.witness.defaults },
          comparison: { ...defaults.comparison, ...parsed.witness.comparison }
        };
      }
    } catch {
      // Config file not found or not parseable — try next location
    }
  }

  return defaults;
}

// Load configuration
const config = await loadConfig();

// Initialize core components using values from config
const executor = new HttpExecutor(config.defaults);
const store = new InteractionStore(config.storage.path);

// Initialize storage on startup
await store.initialize();

// Log alpha version warning to stderr (doesn't interfere with MCP protocol on stdout)
console.error('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
console.error('⚠️  WITNESS MCP SERVER - ALPHA VERSION');
console.error('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
console.error('This software is in ALPHA stage and under active development.');
console.error('Features may change, and breaking changes may occur without notice.');
console.error('Use in production environments at your own risk.');
console.error('━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━');
console.error('');

const context: ToolContext = {
  executor,
  store,
  compareIgnoreFields: config.comparison?.defaultIgnoreFields
};

// Create MCP server
const server = new Server(
  {
    name: 'witness-mcp',
    version: '0.1.0-alpha',
  },
  {
    capabilities: {
      tools: {},
    },
  }
);

// Tool definitions
const TOOLS = [
  {
    name: 'witness/record',
    description: 'Execute an HTTP request and capture the full interaction. Returns a WitnessId that can be used for replay and comparison.',
    inputSchema: {
      type: 'object',
      properties: {
        target: {
          type: 'string',
          description: 'Base URL of the target API (e.g., https://api.example.com)'
        },
        method: {
          type: 'string',
          description: 'HTTP method (GET, POST, PUT, DELETE, PATCH)',
          enum: ['GET', 'POST', 'PUT', 'DELETE', 'PATCH', 'HEAD', 'OPTIONS']
        },
        path: {
          type: 'string',
          description: 'Request path (e.g., /api/loans)'
        },
        headers: {
          type: 'object',
          description: 'HTTP headers as key-value pairs',
          additionalProperties: { type: 'string' }
        },
        body: {
          description: 'Request body (JSON object, string, or omit for no body)'
        },
        options: {
          type: 'object',
          description: 'Recording options',
          properties: {
            tag: {
              type: 'string',
              description: 'Tag for this interaction (used in WitnessId)'
            },
            sessionId: {
              type: 'string',
              description: 'Session ID to group related interactions'
            },
            description: {
              type: 'string',
              description: 'Human-readable description of what this interaction tests'
            },
            timeoutMs: {
              type: 'number',
              description: 'Request timeout in milliseconds (default: 30000)'
            },
            followRedirects: {
              type: 'boolean',
              description: 'Whether to follow HTTP redirects (default: true)'
            }
          }
        }
      },
      required: ['target', 'method', 'path']
    }
  },
  {
    name: 'witness/replay',
    description: 'Replay a previously recorded interaction against a different target. Useful for testing API migrations, version upgrades, or comparing environments.',
    inputSchema: {
      type: 'object',
      properties: {
        witnessId: {
          type: 'string',
          description: 'The WitnessId of the interaction to replay'
        },
        target: {
          type: 'string',
          description: 'New target URL to replay against'
        },
        options: {
          type: 'object',
          description: 'Replay options',
          properties: {
            tag: {
              type: 'string',
              description: 'Tag for the replay interaction'
            },
            sessionId: {
              type: 'string',
              description: 'Session ID for the replay'
            },
            overrideHeaders: {
              type: 'object',
              description: 'Headers to override in the replay',
              additionalProperties: { type: 'string' }
            }
          }
        }
      },
      required: ['witnessId', 'target']
    }
  },
  {
    name: 'witness/inspect',
    description: 'View the full details of a recorded interaction, including request, response, headers, and metadata.',
    inputSchema: {
      type: 'object',
      properties: {
        witnessId: {
          type: 'string',
          description: 'The WitnessId to inspect'
        },
        sessionId: {
          type: 'string',
          description: 'Optional session ID to narrow the search'
        }
      },
      required: ['witnessId']
    }
  },
  {
    name: 'witness/list',
    description: 'List recorded sessions or interactions within a session. Use without parameters to list all sessions, or provide a sessionId to list interactions in that session.',
    inputSchema: {
      type: 'object',
      properties: {
        sessionId: {
          type: 'string',
          description: 'Optional session ID to list interactions from a specific session'
        },
        limit: {
          type: 'number',
          description: 'Maximum number of results to return (default: 50)',
          default: 50
        }
      }
    }
  },
  {
    name: 'witness/compare',
    description: 'Compare two recorded interactions and report differences in status code, response headers, and response body. Useful for API migration testing and regression detection.',
    inputSchema: {
      type: 'object',
      properties: {
        witnessId1: {
          type: 'string',
          description: 'WitnessId of the first (original) interaction'
        },
        witnessId2: {
          type: 'string',
          description: 'WitnessId of the second (replay) interaction'
        },
        sessionId1: {
          type: 'string',
          description: 'Optional session ID to locate the first interaction'
        },
        sessionId2: {
          type: 'string',
          description: 'Optional session ID to locate the second interaction'
        },
        ignoreFields: {
          type: 'array',
          description: 'Response body field names to exclude from comparison (e.g. ["timestamp", "requestId"])',
          items: { type: 'string' }
        }
      },
      required: ['witnessId1', 'witnessId2']
    }
  }
];

// Register tool handlers
server.setRequestHandler(ListToolsRequestSchema, async () => {
  return {
    tools: TOOLS
  };
});

server.setRequestHandler(CallToolRequestSchema, async (request) => {
  const { name, arguments: args } = request.params;

  try {
    switch (name) {
      case 'witness/record':
        return await recordTool(args as unknown as Parameters<typeof recordTool>[0], context);
      case 'witness/replay':
        return await replayTool(args as unknown as Parameters<typeof replayTool>[0], context);
      case 'witness/inspect':
        return await inspectTool(args as unknown as Parameters<typeof inspectTool>[0], context);
      case 'witness/list':
        return await listTool(args as unknown as Parameters<typeof listTool>[0], context);
      case 'witness/compare':
        return await compareTool(args as unknown as Parameters<typeof compareTool>[0], context);
      default:
        return {
          content: [{
            type: 'text',
            text: JSON.stringify({ error: `Unknown tool: ${name}` })
          }],
          isError: true
        };
    }
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : String(error);
    const stack = error instanceof Error ? error.stack : undefined;
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: 'Internal error',
          message,
          stack
        }, null, 2)
      }],
      isError: true
    };
  }
});

// Start the server
async function main() {
  const transport = new StdioServerTransport();
  await server.connect(transport);
  
  // Log to stderr so it doesn't interfere with MCP protocol on stdout
  console.error('Witness MCP Server running on stdio');
}

main().catch((error) => {
  console.error('Fatal error:', error);
  process.exit(1);
});
