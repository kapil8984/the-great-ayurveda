const http = require('http');

console.log('Sending GET http://localhost:5000/ ...');
const start = Date.now();
http.get('http://localhost:5000/', (res) => {
    console.log(`Status: ${res.statusCode} (${Date.now() - start}ms)`);
    let body = '';
    res.on('data', chunk => body += chunk);
    res.on('end', () => {
        console.log(`Received body length: ${body.length} (${Date.now() - start}ms)`);
        console.log(`Contains 'About this project': ${body.includes('About this project')}`);
    });
}).on('error', (err) => {
    console.error(`Error: ${err.message}`);
});
