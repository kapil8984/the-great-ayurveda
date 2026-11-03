const https = require('https');
const http = require('http');

function fetch(url, headers = {}) {
  return new Promise((resolve, reject) => {
    const parsed = new URL(url);
    const client = parsed.protocol === 'https:' ? https : http;
    const req = client.get(parsed, {
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
        'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8',
        ...headers
      }
    }, res => {
      if ([301, 302, 307, 308].includes(res.statusCode) && res.headers.location) {
        return resolve(fetch(new URL(res.headers.location, url).href, headers));
      }
      let chunks = [];
      res.on('data', c => chunks.push(c));
      res.on('end', () => resolve({ status: res.statusCode, headers: res.headers, body: Buffer.concat(chunks).toString('utf-8') }));
    });
    req.on('error', reject);
    req.setTimeout(15000, () => { req.destroy(); reject(new Error('Timeout')); });
  });
}

async function test() {
  console.log('Testing Patanjali:');
  try {
    const res = await fetch('https://www.patanjaliayurved.net/search/dant%20kanti');
    console.log('Patanjali status:', res.status);
    const imgMatches = [...res.body.matchAll(/<img[^>]+src=["']([^"']+)["']/gi)].map(m => m[1]);
    console.log('Sample images:', imgMatches.filter(s => s.includes('product') || s.includes('upload')).slice(0, 5));
    const prodLinks = [...res.body.matchAll(/href=["']([^"']*\/product\/[^"']+)["']/gi)].map(m => m[1]);
    console.log('Sample product links:', prodLinks.slice(0, 5));
  } catch (err) {
    console.error('Patanjali error:', err.message);
  }

  console.log('\nTesting Kottakkal:');
  try {
    const res = await fetch('https://www.aryavaidyasala.com/');
    console.log('Kottakkal status:', res.status);
    const kottakkalShopRes = await fetch('https://shop.aryavaidyasala.com/search/suggest.json?q=chyawanprash&resources[type]=product');
    console.log('shop.aryavaidyasala.com suggest status:', kottakkalShopRes.status, 'isJson:', kottakkalShopRes.body.trim().startsWith('{'));
  } catch (err) {
    console.error('Kottakkal error:', err.message);
  }
}

test();

