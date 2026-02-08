/**
 * HttpExecutor - Executes HTTP requests and captures responses
 */

import axios, { AxiosRequestConfig, AxiosResponse } from 'axios';
import { Interaction, RecordOptions } from '../types/index.js';
import { generateWitnessId } from './witnessId.js';

export interface ExecuteRequestParams {
  target: string;
  method: string;
  path: string;
  headers?: Record<string, string>;
  body?: any;
  options?: RecordOptions;
}

export interface ExecuteResult {
  interaction: Interaction;
  statusCode: number;
  durationMs: number;
  responseBody: any;
  responseHeaders: Record<string, string>;
}

export class HttpExecutor {
  /**
   * Execute an HTTP request and capture the full interaction
   */
  async execute(params: ExecuteRequestParams): Promise<ExecuteResult> {
    const {
      target,
      method,
      path,
      headers = {},
      body,
      options = {}
    } = params;

    // Build full URL
    const url = this.buildUrl(target, path);

    // Prepare request configuration
    const config: AxiosRequestConfig = {
      method: method.toUpperCase(),
      url,
      headers: this.prepareHeaders(headers, body),
      timeout: options.timeoutMs || 30000,
      maxRedirects: options.followRedirects !== false ? 5 : 0,
      validateStatus: () => true, // Don't throw on any status code
    };

    // Add body for methods that support it
    if (body && ['POST', 'PUT', 'PATCH'].includes(method.toUpperCase())) {
      config.data = body;
    }

    // Execute request and measure duration
    const startTime = Date.now();
    let response: AxiosResponse;
    
    try {
      response = await axios(config);
    } catch (error: any) {
      // Handle network errors
      const durationMs = Date.now() - startTime;
      throw new Error(`HTTP request failed: ${error.message} (${durationMs}ms)`);
    }

    const durationMs = Date.now() - startTime;

    // Generate WitnessId
    const tag = options.tag || 'interaction';
    const sessionId = options.sessionId || this.generateSessionId();
    const witnessId = generateWitnessId(tag, method.toUpperCase(), path, body);

    // Build interaction record
    const interaction: Interaction = {
      witnessId,
      sessionId,
      timestamp: new Date().toISOString(),
      request: {
        method: method.toUpperCase(),
        url,
        path,
        headers: config.headers as Record<string, string>,
        body,
        contentType: this.getContentType(config.headers),
      },
      response: {
        statusCode: response.status,
        headers: response.headers as Record<string, string>,
        body: response.data,
        contentType: this.getContentType(response.headers),
        durationMs,
      },
      metadata: {
        tags: options.tag ? [options.tag] : [],
        description: options.description,
      },
    };

    return {
      interaction,
      statusCode: response.status,
      durationMs,
      responseBody: response.data,
      responseHeaders: response.headers as Record<string, string>,
    };
  }

  /**
   * Build full URL from target and path
   */
  private buildUrl(target: string, path: string): string {
    // Remove trailing slash from target
    const baseUrl = target.replace(/\/$/, '');
    
    // Ensure path starts with slash
    const normalizedPath = path.startsWith('/') ? path : `/${path}`;
    
    return `${baseUrl}${normalizedPath}`;
  }

  /**
   * Prepare headers with content-type for JSON bodies
   */
  private prepareHeaders(
    headers: Record<string, string>,
    body?: any
  ): Record<string, string> {
    const result = { ...headers };

    // Auto-set Content-Type for JSON bodies if not specified
    if (body && typeof body === 'object' && !result['Content-Type'] && !result['content-type']) {
      result['Content-Type'] = 'application/json';
    }

    return result;
  }

  /**
   * Extract content-type from headers
   */
  private getContentType(headers: any): string | undefined {
    if (!headers) return undefined;
    return headers['content-type'] || headers['Content-Type'];
  }

  /**
   * Generate a default session ID
   */
  private generateSessionId(): string {
    const now = new Date();
    const date = now.toISOString().split('T')[0];
    return `session-${date}`;
  }
}
