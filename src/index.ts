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

import { HttpExecutor } from './core/httpExecutor.js';
import { InteractionStore } from './storage/interactionStore.js';
import { recordTool, replayTool, inspectTool, listTool, ToolContext } from './tools/index.js';

// Initialize core components
const executor = new HttpExecutor();
const store = new InteractionStore();

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
  store
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
        return await recordTool(args, context);
      case 'witness/replay':
        return await replayTool(args, context);
      case 'witness/inspect':
        return await inspectTool(args, context);
      case 'witness/list':
        return await listTool(args, context);
      default:
        return {
          content: [{
            type: 'text',
            text: JSON.stringify({ error: `Unknown tool: ${name}` })
          }],
          isError: true
        };
    }
  } catch (error: any) {
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: 'Internal error',
          message: error.message,
          stack: error.stack
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
