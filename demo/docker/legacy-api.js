const http = require('http');

const server = http.createServer((req, res) => {
  if (req.method === 'GET' && req.url === '/api/products/1') {
    res.writeHead(200, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ id: 1, name: 'Widget', unit_price: 9.99, stock: 100 }));
  } else if (req.method === 'POST' && req.url === '/api/orders') {
    let body = '';
    req.on('data', chunk => { body += chunk; });
    req.on('end', () => {
      res.writeHead(201, { 'Content-Type': 'application/json' });
      res.end(JSON.stringify({ order_id: 1001, product_id: 1, qty: 2, total_price: 19.98, status: 'pending' }));
    });
  } else {
    res.writeHead(404, { 'Content-Type': 'application/json' });
    res.end(JSON.stringify({ error: 'Not found' }));
  }
});

server.listen(3001, () => {
  console.log('Legacy API running on port 3001');
});
