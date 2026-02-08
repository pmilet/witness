#!/usr/bin/env node
/**
 * Unit tests for core Witness components
 */

import { generateWitnessId } from './dist/core/witnessId.js';
import { InteractionStore } from './dist/storage/interactionStore.js';
import { promises as fs } from 'fs';
import path from 'path';

const TEST_STORE_PATH = './test-witness-store';

async function runTests() {
  console.log('Running Witness Core Unit Tests...\n');
  
  let passed = 0;
  let failed = 0;

  // Test 1: WitnessId generation without body
  try {
    const id = generateWitnessId('test', 'GET', '/api/users', undefined);
    const parts = id.split('_');
    
    if (parts[0] === 'test' && 
        parts[1] === 'GET' && 
        parts[2] === 'api-users' && 
        parts[3] === '00000000' &&
        parts[4].match(/\d{8}T\d{4}/)) {
      console.log('✅ Test 1: WitnessId generation (no body)');
      console.log(`   Generated: ${id}`);
      passed++;
    } else {
      throw new Error('Invalid WitnessId format');
    }
  } catch (error) {
    console.log('❌ Test 1: WitnessId generation (no body)');
    console.log(`   Error: ${error.message}`);
    failed++;
  }

  // Test 2: WitnessId generation with body
  try {
    const body = { name: 'Alice', email: 'alice@example.com' };
    const id = generateWitnessId('create-user', 'POST', '/api/users', body);
    const parts = id.split('_');
    
    if (parts[0] === 'create-user' && 
        parts[1] === 'POST' && 
        parts[2] === 'api-users' && 
        parts[3].length === 8 &&
        parts[3] !== '00000000') {
      console.log('✅ Test 2: WitnessId generation (with body)');
      console.log(`   Generated: ${id}`);
      passed++;
    } else {
      throw new Error('Invalid WitnessId format');
    }
  } catch (error) {
    console.log('❌ Test 2: WitnessId generation (with body)');
    console.log(`   Error: ${error.message}`);
    failed++;
  }

  // Test 3: Deterministic WitnessId (same input = same hash)
  try {
    const body = { test: 'data' };
    const id1 = generateWitnessId('tag', 'POST', '/path', body);
    const id2 = generateWitnessId('tag', 'POST', '/path', body);
    
    // Should be identical except for timestamp
    const hash1 = id1.split('_')[3];
    const hash2 = id2.split('_')[3];
    
    if (hash1 === hash2) {
      console.log('✅ Test 3: Deterministic hashing');
      console.log(`   Hash: ${hash1}`);
      passed++;
    } else {
      throw new Error('Hashes should be identical for same body');
    }
  } catch (error) {
    console.log('❌ Test 3: Deterministic hashing');
    console.log(`   Error: ${error.message}`);
    failed++;
  }

  // Test 4: Path slug generation
  try {
    const id1 = generateWitnessId('test', 'GET', '/api/v1/users/123/profile', undefined);
    const pathSlug = id1.split('_')[2];
    
    if (pathSlug === 'api-v1-users-123-profile') {
      console.log('✅ Test 4: Path slug generation');
      console.log(`   Slug: ${pathSlug}`);
      passed++;
    } else {
      throw new Error(`Unexpected path slug: ${pathSlug}`);
    }
  } catch (error) {
    console.log('❌ Test 4: Path slug generation');
    console.log(`   Error: ${error.message}`);
    failed++;
  }

  // Test 5: InteractionStore initialization
  try {
    const store = new InteractionStore(TEST_STORE_PATH);
    await store.initialize();
    
    // Check that directories were created
    const sessionsPath = path.join(TEST_STORE_PATH, 'sessions');
    const suitesPath = path.join(TEST_STORE_PATH, 'suites');
    
    const sessionsExist = await fs.stat(sessionsPath).then(() => true).catch(() => false);
    const suitesExist = await fs.stat(suitesPath).then(() => true).catch(() => false);
    
    if (sessionsExist && suitesExist) {
      console.log('✅ Test 5: InteractionStore initialization');
      console.log(`   Store path: ${TEST_STORE_PATH}`);
      passed++;
    } else {
      throw new Error('Required directories not created');
    }
  } catch (error) {
    console.log('❌ Test 5: InteractionStore initialization');
    console.log(`   Error: ${error.message}`);
    failed++;
  }

  // Test 6: Save and load interaction
  try {
    const store = new InteractionStore(TEST_STORE_PATH);
    const interaction = {
      witnessId: 'test_GET_api-test_00000000_20260208T1430',
      sessionId: 'test-session',
      timestamp: new Date().toISOString(),
      request: {
        method: 'GET',
        url: 'https://example.com/api/test',
        path: '/api/test',
        headers: { 'User-Agent': 'Witness' },
        contentType: 'application/json'
      },
      response: {
        statusCode: 200,
        headers: { 'content-type': 'application/json' },
        body: { success: true },
        contentType: 'application/json',
        durationMs: 123
      },
      metadata: {
        tags: ['test'],
        description: 'Test interaction'
      }
    };
    
    await store.saveInteraction(interaction);
    const loaded = await store.loadInteraction(interaction.witnessId, 'test-session');
    
    if (loaded && loaded.witnessId === interaction.witnessId) {
      console.log('✅ Test 6: Save and load interaction');
      console.log(`   WitnessId: ${loaded.witnessId}`);
      passed++;
    } else {
      throw new Error('Failed to load saved interaction');
    }
  } catch (error) {
    console.log('❌ Test 6: Save and load interaction');
    console.log(`   Error: ${error.message}`);
    failed++;
  }

  // Test 7: List sessions
  try {
    const store = new InteractionStore(TEST_STORE_PATH);
    const sessions = await store.listSessions();
    
    if (sessions.length > 0 && sessions[0].sessionId === 'test-session') {
      console.log('✅ Test 7: List sessions');
      console.log(`   Found ${sessions.length} session(s)`);
      passed++;
    } else {
      throw new Error('Session not found in list');
    }
  } catch (error) {
    console.log('❌ Test 7: List sessions');
    console.log(`   Error: ${error.message}`);
    failed++;
  }

  // Clean up test store
  try {
    await fs.rm(TEST_STORE_PATH, { recursive: true, force: true });
    console.log('\n🧹 Cleaned up test storage');
  } catch (error) {
    console.log('\n⚠️  Failed to clean up test storage');
  }

  // Summary
  console.log('\n' + '='.repeat(50));
  console.log(`Tests completed: ${passed} passed, ${failed} failed`);
  console.log('='.repeat(50));

  if (failed === 0) {
    console.log('\n🎉 All tests passed!');
    process.exit(0);
  } else {
    console.log('\n❌ Some tests failed');
    process.exit(1);
  }
}

runTests().catch((error) => {
  console.error('Test suite failed:', error);
  process.exit(1);
});
