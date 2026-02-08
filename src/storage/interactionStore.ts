/**
 * InteractionStore - Manages storage of interactions to local filesystem
 */

import { promises as fs } from 'fs';
import path from 'path';
import { Interaction, Session } from '../types/index.js';

export class InteractionStore {
  private basePath: string;

  constructor(basePath: string = './witness-store') {
    this.basePath = basePath;
  }

  /**
   * Initialize storage directory structure
   */
  async initialize(): Promise<void> {
    await this.ensureDir(this.basePath);
    await this.ensureDir(path.join(this.basePath, 'sessions'));
    await this.ensureDir(path.join(this.basePath, 'suites'));
    await this.ensureDir(path.join(this.basePath, 'mocks'));
    await this.ensureDir(path.join(this.basePath, 'specs'));
  }

  /**
   * Save an interaction to storage
   */
  async saveInteraction(interaction: Interaction): Promise<void> {
    const sessionPath = this.getSessionPath(interaction.sessionId);
    await this.ensureDir(sessionPath);

    const interactionsPath = path.join(sessionPath, 'interactions');
    await this.ensureDir(interactionsPath);

    // Save interaction file
    const interactionFile = path.join(interactionsPath, `${interaction.witnessId}.json`);
    await fs.writeFile(interactionFile, JSON.stringify(interaction, null, 2), 'utf-8');

    // Update session metadata
    await this.updateSessionMetadata(interaction.sessionId, interaction);
  }

  /**
   * Load an interaction by witnessId
   */
  async loadInteraction(witnessId: string, sessionId?: string): Promise<Interaction | null> {
    // If sessionId provided, try that session first
    if (sessionId) {
      const interaction = await this.loadFromSession(witnessId, sessionId);
      if (interaction) return interaction;
    }

    // Otherwise search all sessions
    const sessions = await this.listSessions();
    for (const session of sessions) {
      const interaction = await this.loadFromSession(witnessId, session.sessionId);
      if (interaction) return interaction;
    }

    return null;
  }

  /**
   * Load interaction from a specific session
   */
  private async loadFromSession(witnessId: string, sessionId: string): Promise<Interaction | null> {
    const interactionFile = path.join(
      this.getSessionPath(sessionId),
      'interactions',
      `${witnessId}.json`
    );

    try {
      const content = await fs.readFile(interactionFile, 'utf-8');
      return JSON.parse(content);
    } catch (error) {
      return null;
    }
  }

  /**
   * List all sessions
   */
  async listSessions(): Promise<Session[]> {
    const sessionsPath = path.join(this.basePath, 'sessions');
    
    try {
      const entries = await fs.readdir(sessionsPath, { withFileTypes: true });
      const sessions: Session[] = [];

      for (const entry of entries) {
        if (entry.isDirectory()) {
          const sessionFile = path.join(sessionsPath, entry.name, 'session.json');
          try {
            const content = await fs.readFile(sessionFile, 'utf-8');
            sessions.push(JSON.parse(content));
          } catch (error) {
            // Session metadata doesn't exist yet, skip
          }
        }
      }

      return sessions.sort((a, b) => b.createdAt.localeCompare(a.createdAt));
    } catch (error) {
      return [];
    }
  }

  /**
   * List interactions in a session
   */
  async listInteractions(sessionId: string): Promise<Interaction[]> {
    const interactionsPath = path.join(this.getSessionPath(sessionId), 'interactions');

    try {
      const files = await fs.readdir(interactionsPath);
      const interactions: Interaction[] = [];

      for (const file of files) {
        if (file.endsWith('.json')) {
          const content = await fs.readFile(path.join(interactionsPath, file), 'utf-8');
          interactions.push(JSON.parse(content));
        }
      }

      return interactions.sort((a, b) => b.timestamp.localeCompare(a.timestamp));
    } catch (error) {
      return [];
    }
  }

  /**
   * Get the path for a session directory
   */
  private getSessionPath(sessionId: string): string {
    return path.join(this.basePath, 'sessions', sessionId);
  }

  /**
   * Update session metadata
   */
  private async updateSessionMetadata(sessionId: string, interaction: Interaction): Promise<void> {
    const sessionPath = this.getSessionPath(sessionId);
    const sessionFile = path.join(sessionPath, 'session.json');

    let session: Session;

    try {
      const content = await fs.readFile(sessionFile, 'utf-8');
      session = JSON.parse(content);
      session.interactionCount += 1;
      
      // Merge tags
      for (const tag of interaction.metadata.tags) {
        if (!session.tags.includes(tag)) {
          session.tags.push(tag);
        }
      }
    } catch (error) {
      // Create new session metadata
      session = {
        sessionId,
        createdAt: new Date().toISOString(),
        tags: interaction.metadata.tags,
        interactionCount: 1,
      };
    }

    await fs.writeFile(sessionFile, JSON.stringify(session, null, 2), 'utf-8');
  }

  /**
   * Ensure directory exists
   */
  private async ensureDir(dirPath: string): Promise<void> {
    try {
      await fs.mkdir(dirPath, { recursive: true });
    } catch (error) {
      // Directory might already exist
    }
  }
}
