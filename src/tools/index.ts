/**
 * MCP Tools for Witness Server
 */

import { HttpExecutor } from '../core/httpExecutor.js';
import { InteractionStore } from '../storage/interactionStore.js';

export interface ToolContext {
  executor: HttpExecutor;
  store: InteractionStore;
}

/**
 * witness/record - Execute an HTTP request and capture the interaction
 */
export async function recordTool(args: any, context: ToolContext) {
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
  } catch (error: any) {
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: error.message,
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
export async function replayTool(args: any, context: ToolContext) {
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
  } catch (error: any) {
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: error.message,
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
export async function inspectTool(args: any, context: ToolContext) {
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
  } catch (error: any) {
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: error.message,
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
export async function listTool(args: any, context: ToolContext) {
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
            interactions: limited.map(i => ({
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
  } catch (error: any) {
    return {
      content: [{
        type: 'text',
        text: JSON.stringify({
          error: error.message
        }, null, 2)
      }],
      isError: true
    };
  }
}
