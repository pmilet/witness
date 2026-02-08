/**
 * Core data types for Witness MCP Server
 * Based on witness-mcp-server-spec.md Section 3
 */

/**
 * Represents a recorded HTTP interaction
 */
export interface Interaction {
  // Identity
  witnessId: string;
  sessionId: string;
  timestamp: string; // ISO 8601

  // Request
  request: {
    method: string;
    url: string;
    path: string;
    headers: Record<string, string>;
    body?: any;
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
    tags: string[];
    description?: string;
    openApiOperationId?: string;
    chainStep?: number;
    chainId?: string;
  };

  // Outbound calls (if proxy capture enabled)
  outboundCalls?: Interaction[];
}

/**
 * Configuration for recording an interaction
 */
export interface RecordOptions {
  tag?: string;
  sessionId?: string;
  validateSchema?: boolean;
  captureOutbound?: boolean;
  followRedirects?: boolean;
  timeoutMs?: number;
  description?: string;
}

/**
 * Configuration for replaying an interaction
 */
export interface ReplayOptions {
  tag?: string;
  overrideHeaders?: Record<string, string>;
  mockOutbound?: boolean;
  sessionId?: string;
}

/**
 * Session metadata
 */
export interface Session {
  sessionId: string;
  createdAt: string;
  tags: string[];
  interactionCount: number;
  description?: string;
}

/**
 * Configuration for the Witness server
 */
export interface WitnessConfig {
  storage: {
    type: 'local' | 'azure-blob';
    path: string;
    azureConnectionString?: string;
  };
  defaults: {
    timeoutMs: number;
    followRedirects: boolean;
  };
  comparison?: {
    defaultIgnoreFields?: string[];
    defaultNumericTolerance?: number;
  };
}
