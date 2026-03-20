/**
 * MCP Tools for Witness Server
 */

import { HttpExecutor } from '../core/httpExecutor.js';
import { InteractionStore } from '../storage/interactionStore.js';
import { Interaction, RecordOptions, ReplayOptions } from '../types/index.js';

export interface ToolContext {
  executor: HttpExecutor;
  store: InteractionStore;
  compareIgnoreFields?: string[];
}

interface RecordArgs {
  target: string;
  method: string;
  path: string;
  headers?: Record<string, string>;
  body?: unknown;
  options?: RecordOptions;
}

interface ReplayArgs {
  witnessId: string;
  target: string;
  options?: ReplayOptions;
}

interface InspectArgs {
  witnessId: string;
  sessionId?: string;
}

interface ListArgs {
  sessionId?: string;
  limit?: number;
}

interface CompareArgs {
  witnessId1: string;
  witnessId2: string;
  sessionId1?: string;
  sessionId2?: string;
  ignoreFields?: string[];
}

type ToolResult = {
  content: Array<{ type: string; text: string }>;
  isError?: true;
};

/**
 * witness/record - Execute an HTTP request and capture the interaction
 */
export async function recordTool(args: RecordArgs, context: ToolContext): Promise<ToolResult> {
  const {
    target,
    method,
    path,
    headers = {},
    body,
    options = {}
  } = args;

  // Validate required parameters
  if (!target || !method || !path) {
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: 'Missing required parameters',
          required: ['target', 'method', 'path'],
          received: { target, method, path }
        }, null, 2)
      }]
    };
  }

  try {
    // Execute the HTTP request
    const result = await context.executor.execute({
      target,
      method,
      path,
      headers,
      body,
      options
    });

    // Save to storage
    await context.store.saveInteraction(result.interaction);

    // Return formatted response
    const response = {
      witnessId: result.interaction.witnessId,
      sessionId: result.interaction.sessionId,
      statusCode: result.statusCode,
      durationMs: result.durationMs,
      responseBody: result.responseBody,
      responseHeaders: result.responseHeaders,
      stored: true
    };

    return {
      content: [{
        type: 'text',
        text: JSON.stringify(response, null, 2)
      }]
    };
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : String(error);
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: message,
          target,
          method,
          path
        }, null, 2)
      }],
      isError: true
    };
  }
}

/**
 * witness/replay - Replay a recorded interaction against a different target
 */
export async function replayTool(args: ReplayArgs, context: ToolContext): Promise<ToolResult> {
  const { witnessId, target, options = {} } = args;

  // Validate required parameters
  if (!witnessId || !target) {
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: 'Missing required parameters',
          required: ['witnessId', 'target'],
          received: { witnessId, target }
        }, null, 2)
      }]
    };
  }

  try {
    // Load the original interaction
    const original = await context.store.loadInteraction(witnessId, options.sessionId);

    if (!original) {
      return {
        content: [{
          type: 'text',
          text: JSON.stringify({
            error: `Interaction not found: ${witnessId}`
          }, null, 2)
        }],
        isError: true
      };
    }

    // Replay the request
    const headers = {
      ...original.request.headers,
      ...(options.overrideHeaders || {})
    };

    const result = await context.executor.execute({
      target,
      method: original.request.method,
      path: original.request.path,
      headers,
      body: original.request.body,
      options: {
        tag: options.tag || `replay-${original.metadata.tags[0] || 'interaction'}`,
        sessionId: options.sessionId || original.sessionId
      }
    });

    // Save replay to storage
    await context.store.saveInteraction(result.interaction);

    // Return response
    const response = {
      originalWitnessId: witnessId,
      replayWitnessId: result.interaction.witnessId,
      statusCode: result.statusCode,
      durationMs: result.durationMs,
      responseBody: result.responseBody,
      stored: true
    };

    return {
      content: [{
        type: 'text',
        text: JSON.stringify(response, null, 2)
      }]
    };
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : String(error);
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: message,
          witnessId,
          target
        }, null, 2)
      }],
      isError: true
    };
  }
}

/**
 * witness/inspect - View details of a recorded interaction
 */
export async function inspectTool(args: InspectArgs, context: ToolContext): Promise<ToolResult> {
  const { witnessId, sessionId } = args;

  if (!witnessId) {
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: 'Missing required parameter: witnessId'
        }, null, 2)
      }]
    };
  }

  try {
    const interaction = await context.store.loadInteraction(witnessId, sessionId);

    if (!interaction) {
      return {
        content: [{
          type: 'text',
          text: JSON.stringify({
            error: `Interaction not found: ${witnessId}`
          }, null, 2)
        }],
        isError: true
      };
    }

    return {
      content: [{
        type: 'text',
        text: JSON.stringify(interaction, null, 2)
      }]
    };
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : String(error);
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: message,
          witnessId
        }, null, 2)
      }],
      isError: true
    };
  }
}

/**
 * witness/list - List recorded sessions and interactions
 */
export async function listTool(args: ListArgs, context: ToolContext): Promise<ToolResult> {
  const { sessionId, limit = 50 } = args;

  try {
    if (sessionId) {
      // List interactions in a specific session
      const interactions = await context.store.listInteractions(sessionId);
      const limited = interactions.slice(0, limit);

      return {
        content: [{
          type: 'text',
          text: JSON.stringify({
            sessionId,
            count: limited.length,
            total: interactions.length,
            interactions: limited.map((i: Interaction) => ({
              witnessId: i.witnessId,
              timestamp: i.timestamp,
              method: i.request.method,
              path: i.request.path,
              statusCode: i.response.statusCode,
              durationMs: i.response.durationMs,
              tags: i.metadata.tags
            }))
          }, null, 2)
        }]
      };
    } else {
      // List all sessions
      const sessions = await context.store.listSessions();
      const limited = sessions.slice(0, limit);

      return {
        content: [{
          type: 'text',
          text: JSON.stringify({
            count: limited.length,
            total: sessions.length,
            sessions: limited
          }, null, 2)
        }]
      };
    }
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : String(error);
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: message
        }, null, 2)
      }],
      isError: true
    };
  }
}

/**
 * Deeply compare two values and collect differences
 */
function collectDiffs(
  a: unknown,
  b: unknown,
  path: string,
  ignoreFields: Set<string>,
  diffs: Array<{ path: string; original: unknown; replay: unknown }>
): void {
  // Skip ignored fields
  const fieldName = path.split('.').pop() ?? '';
  if (ignoreFields.has(fieldName) || ignoreFields.has(path)) return;

  if (a === b) return;

  if (
    a !== null && b !== null &&
    typeof a === 'object' && typeof b === 'object' &&
    !Array.isArray(a) && !Array.isArray(b)
  ) {
    const aObj = a as Record<string, unknown>;
    const bObj = b as Record<string, unknown>;
    const keys = new Set([...Object.keys(aObj), ...Object.keys(bObj)]);
    for (const key of keys) {
      collectDiffs(aObj[key], bObj[key], path ? `${path}.${key}` : key, ignoreFields, diffs);
    }
    return;
  }

  diffs.push({ path: path || '(root)', original: a, replay: b });
}

/**
 * witness/compare - Diff two recorded interactions
 */
export async function compareTool(args: CompareArgs, context: ToolContext): Promise<ToolResult> {
  const { witnessId1, witnessId2, sessionId1, sessionId2, ignoreFields = [] } = args;

  if (!witnessId1 || !witnessId2) {
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: 'Missing required parameters',
          required: ['witnessId1', 'witnessId2'],
          received: { witnessId1, witnessId2 }
        }, null, 2)
      }]
    };
  }

  try {
    const [interaction1, interaction2] = await Promise.all([
      context.store.loadInteraction(witnessId1, sessionId1),
      context.store.loadInteraction(witnessId2, sessionId2)
    ]);

    if (!interaction1) {
      return {
        content: [{
          type: 'text',
          text: JSON.stringify({ error: `Interaction not found: ${witnessId1}` }, null, 2)
        }],
        isError: true
      };
    }

    if (!interaction2) {
      return {
        content: [{
          type: 'text',
          text: JSON.stringify({ error: `Interaction not found: ${witnessId2}` }, null, 2)
        }],
        isError: true
      };
    }

    // Merge ignore fields: tool arg + context defaults
    const allIgnoreFields = new Set([
      ...(context.compareIgnoreFields ?? []),
      ...ignoreFields
    ]);

    // Compare status codes
    const statusMatch = interaction1.response.statusCode === interaction2.response.statusCode;

    // Compare response bodies
    const bodyDiffs: Array<{ path: string; original: unknown; replay: unknown }> = [];
    collectDiffs(interaction1.response.body, interaction2.response.body, '', allIgnoreFields, bodyDiffs);

    // Compare response headers (case-insensitive keys, skip ignored)
    const headerDiffs: Array<{ header: string; original: string | undefined; replay: string | undefined }> = [];
    const headers1 = Object.fromEntries(
      Object.entries(interaction1.response.headers).map(([k, v]) => [k.toLowerCase(), v])
    );
    const headers2 = Object.fromEntries(
      Object.entries(interaction2.response.headers).map(([k, v]) => [k.toLowerCase(), v])
    );
    const allHeaderKeys = new Set([...Object.keys(headers1), ...Object.keys(headers2)]);
    for (const key of allHeaderKeys) {
      if (allIgnoreFields.has(key)) continue;
      if (headers1[key] !== headers2[key]) {
        headerDiffs.push({ header: key, original: headers1[key], replay: headers2[key] });
      }
    }

    const isMatch = statusMatch && bodyDiffs.length === 0 && headerDiffs.length === 0;

    const result = {
      isMatch,
      witnessId1,
      witnessId2,
      summary: {
        statusCode: {
          match: statusMatch,
          original: interaction1.response.statusCode,
          replay: interaction2.response.statusCode
        },
        body: {
          match: bodyDiffs.length === 0,
          diffCount: bodyDiffs.length,
          diffs: bodyDiffs
        },
        headers: {
          match: headerDiffs.length === 0,
          diffCount: headerDiffs.length,
          diffs: headerDiffs
        }
      },
      requests: {
        original: {
          method: interaction1.request.method,
          url: interaction1.request.url,
          timestamp: interaction1.timestamp
        },
        replay: {
          method: interaction2.request.method,
          url: interaction2.request.url,
          timestamp: interaction2.timestamp
        }
      }
    };

    return {
      content: [{
        type: 'text',
        text: JSON.stringify(result, null, 2)
      }]
    };
  } catch (error: unknown) {
    const message = error instanceof Error ? error.message : String(error);
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: message,
          witnessId1,
          witnessId2
        }, null, 2)
      }],
      isError: true
    };
  }
}
