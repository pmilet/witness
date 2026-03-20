const http = require('http');

const server = http.createServer((req, res) => {
  if (req.method === 'GET' && req.url === '/api/products/1') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ id: 1, name: 'Widget', price: 9.99, currency: 'USD' }));
  } else if (req.method === 'POST' && req.url === '/api/orders') {
    let body = '';
    req.on('data', chunk => { body += chunk; });
    req.on('end', () => {
      res.writeHead(201, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ orderId: 1001, productId: 1, quantity: 2, amount: 19.98, currency: 'USD', state: 'created' }));
    });
  } else {
    res.writeHead(404, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ error: 'Not found' }));
  }
});

server.listen(3002, () => {
  console.log('Modern API running on port 3002');
});
