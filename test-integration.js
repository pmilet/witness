#!/usr/bin/env node
/**
 * Interactive test script for Witness MCP Server
 * Tests the server with real API calls to JSONPlaceholder
 */

import { spawn } from 'child_process';
import { createInterface } from 'readline';

const TEST_CASES = [
  {
    name: 'List Tools',
    request: {
      jsonrpc: '2.0',
      id: 1,
      method: 'tools/list',
      params: {}
    }
  },
  {
    name: 'Record GET Request',
    request: {
      jsonrpc: '2.0',
      id: 2,
      method: 'tools/call',
      params: {
        name: 'witness/record',
        arguments: {
          target: 'https://jsonplaceholder.typicode.com',
          method: 'GET',
          path: '/posts/1',
          options: {
            tag: 'test-get',
            sessionId: 'test-session-manual'
          }
        }
      }
    }
  },
  {
    name: 'List Sessions',
    request: {
      jsonrpc: '2.0',
      id: 3,
      method: 'tools/call',
      params: {
        name: 'witness/list',
        arguments: {}
      }
    }
  }
];

async function runTest() {
  console.log('Starting Witness MCP Server test...\n');

  // Start the MCP server
  const server = spawn('node', ['dist/index.js'], {
    stdio: ['pipe', 'pipe', 'pipe']
  });

  let responseCount = 0;
  let buffer = '';

  server.stdout.on('data', (data) => {
    buffer += data.toString();
    
    // Try to parse complete JSON-RPC messages
    const lines = buffer.split('\n');
    buffer = lines.pop() || ''; // Keep incomplete line in buffer
    
    for (const line of lines) {
      if (line.trim()) {
        try {
          const response = JSON.parse(line);
          responseCount++;
          console.log(`Response ${responseCount}:`);
          console.log(JSON.stringify(response, null, 2));
          console.log('\n' + '='.repeat(80) + '\n');
        } catch (e) {
          // Not a complete JSON object yet
        }
      }
    }
  });

  server.stderr.on('data', (data) => {
    console.error('Server stderr:', data.toString());
  });

  // Send initialize request
  console.log('Sending initialize request...');
  const initRequest = {
    jsonrpc: '2.0',
    id: 0,
    method: 'initialize',
    params: {
      protocolVersion: '2024-11-05',
      capabilities: {},
      clientInfo: {
        name: 'test-client',
        version: '1.0.0'
      }
    }
  };
  server.stdin.write(JSON.stringify(initRequest) + '\n');

  // Wait a bit for initialization
  await new Promise(resolve => setTimeout(resolve, 500));

  // Send test cases
  for (const testCase of TEST_CASES) {
    console.log(`Sending: ${testCase.name}...`);
    server.stdin.write(JSON.stringify(testCase.request) + '\n');
    await new Promise(resolve => setTimeout(resolve, 2000)); // Wait for response
  }

  // Give time for final responses
  await new Promise(resolve => setTimeout(resolve, 1000));

  console.log('\n✅ Test completed successfully!');
  console.log(`Received ${responseCount} responses`);
  
  server.kill();
  process.exit(0);
}

runTest().catch((error) => {
  console.error('Test failed:', error);
  process.exit(1);
});
