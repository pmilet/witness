/**
 * WitnessId Generator
 * Creates deterministic, human-readable identifiers for interactions
 * Format: {tag}_{method}_{path-slug}_{body-hash}_{timestamp}
 */

import { createHash } from 'crypto';

/**
 * Generate a WitnessId from request components
 */
export function generateWitnessId(
  tag: string,
  method: string,
  path: string,
  body?: any
): string {
  const pathSlug = createPathSlug(path);
  const bodyHash = createBodyHash(body);
  const timestamp = createTimestamp();

  return `${tag}_${method}_${pathSlug}_${bodyHash}_${timestamp}`;
}

/**
 * Create a URL-safe slug from a path
 * Example: /api/loans/123 -> api-loans-123
 */
function createPathSlug(path: string): string {
  // Remove leading slash and convert remaining slashes to hyphens
  let slug = path.replace(/^\//, '').replace(/\//g, '-');
  
  // Remove query parameters
  slug = slug.split('?')[0];
  
  // Replace special characters with hyphens
  slug = slug.replace(/[^a-zA-Z0-9-]/g, '-');
  
  // Remove consecutive hyphens
  slug = slug.replace(/-+/g, '-');
  
  // Truncate to 60 characters
  if (slug.length > 60) {
    slug = slug.substring(0, 60);
  }
  
  // Remove trailing hyphen
  slug = slug.replace(/-$/, '');
  
  return slug || 'root';
}

/**
 * Create a hash of the request body
 * Returns first 8 characters of SHA-256 hash, or '00000000' for no body
 */
function createBodyHash(body?: any): string {
  if (!body || (typeof body === 'object' && Object.keys(body).length === 0)) {
    return '00000000';
  }

  const bodyString = typeof body === 'string' ? body : JSON.stringify(body);
  const hash = createHash('sha256').update(bodyString).digest('hex');
  return hash.substring(0, 8);
}

/**
 * Create a compact ISO timestamp
 * Format: 20260208T1430
 */
function createTimestamp(): string {
  const now = new Date();
  const year = now.getUTCFullYear();
  const month = String(now.getUTCMonth() + 1).padStart(2, '0');
  const day = String(now.getUTCDate()).padStart(2, '0');
  const hour = String(now.getUTCHours()).padStart(2, '0');
  const minute = String(now.getUTCMinutes()).padStart(2, '0');
  
  return `${year}${month}${day}T${hour}${minute}`;
}
