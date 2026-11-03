const https = require('https');
const http = require('http');
const fs = require('fs');
const path = require('path');
const sharp = require('../tests/e2e/node_modules/sharp');

function fetchJson(url) {
  return new Promise((resolve) => {
    const parsed = new URL(url);
    const client = parsed.protocol === 'https:' ? https : http;
    const req = client.get(parsed, {
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
        'Accept': 'application/json, text/plain, */*'
      }
    }, res => {
      if ([301, 302, 307, 308].includes(res.statusCode) && res.headers.location) {
        return resolve(fetchJson(new URL(res.headers.location, url).href));
      }
      let d = '';
      res.on('data', c => d += c);
      res.on('end', () => {
        try {
          resolve({ status: res.statusCode, json: JSON.parse(d) });
        } catch(e) {
          resolve({ status: res.statusCode, error: e.message, text: d.slice(0, 300) });
        }
      });
    });
    req.on('error', e => resolve({ error: e.message }));
    req.setTimeout(15000, () => { req.destroy(); resolve({ error: 'Timeout' }); });
  });
}

function fetchBuffer(url) {
  return new Promise((resolve, reject) => {
    if (url.startsWith('//')) url = 'https:' + url;
    const parsed = new URL(url);
    const client = parsed.protocol === 'https:' ? https : http;
    const req = client.get(parsed, {
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)',
        'Accept': 'image/avif,image/webp,image/apng,image/*,*/*;q=0.8'
      }
    }, res => {
      if ([301, 302, 307, 308].includes(res.statusCode) && res.headers.location) {
        return resolve(fetchBuffer(new URL(res.headers.location, url).href));
      }
      let chunks = [];
      res.on('data', c => chunks.push(c));
      res.on('end', () => resolve(Buffer.concat(chunks)));
    });
    req.on('error', reject);
    req.setTimeout(15000, () => { req.destroy(); reject(new Error('Timeout')); });
  });
}

async function testSample() {
  const brands = [
    { brand: 'Himalaya Wellness', host: 'himalayawellness.in', query: 'Liv.52' },
    { brand: 'Dabur India', host: 'www.daburshop.com', query: 'Chyawanprash' },
    { brand: 'Sri Sri Tattva', host: 'www.srisritattva.com', query: 'Moisturiser' },
    { brand: 'Zandu Care', host: 'zanducare.com', query: 'Nityam Churna' },
    { brand: 'Baidyanath', host: 'baidyanath.co.in', query: 'Chyawanprash' },
    { brand: 'Charak Pharma', host: 'charak.com', query: 'M2-Tone' },
    { brand: 'AVP', host: 'avpayurveda.com', query: 'Ksheerabala' },
    { brand: 'Maharishi Ayurveda', host: 'maharishiayurvedaindia.com', query: 'Amrit Kalash' },
    { brand: 'Kottakkal Arya Vaidya Sala', host: 'www.ayurkart.com', query: 'Kottakkal Nilibhringadi' }
  ];

  for (const b of brands) {
    console.log(`\n================== Checking ${b.brand} ==================`);
    const searchUrl = `https://${b.host}/search/suggest.json?q=${encodeURIComponent(b.query)}&resources[type]=product`;
    const res = await fetchJson(searchUrl);
    if (!res.json || !res.json.resources || !res.json.resources.results.products.length) {
      console.log(`❌ No products found for ${b.query} on ${b.host}:`, res);
      continue;
    }
    const p = res.json.resources.results.products[0];
    console.log(`✅ Found: "${p.title}"`);
    console.log(`   URL: ${p.url}`);

    // Fetch full product json for all angles
    const cleanProdUrl = p.url.split('?')[0];
    const fullProdRes = await fetchJson(`https://${b.host}${cleanProdUrl}.js`);
    if (fullProdRes.json && fullProdRes.json.images) {
      console.log(`   Angles/Images count: ${fullProdRes.json.images.length}`);
      fullProdRes.json.images.forEach((img, idx) => console.log(`     Angle ${idx + 1}: ${img}`));

      // Test downloading angle 1 and verifying with sharp
      const firstImg = fullProdRes.json.images[0];
      const buf = await fetchBuffer(firstImg);
      const meta = await sharp(buf).metadata();
      console.log(`   Downloaded First Angle: ${meta.width}x${meta.height}, format: ${meta.format}, size: ${buf.length} bytes`);
    } else {
      console.log(`   Fallback to search image: ${p.image}`);
      if (p.image) {
        const buf = await fetchBuffer(p.image);
        const meta = await sharp(buf).metadata();
        console.log(`   Downloaded Search Image: ${meta.width}x${meta.height}, format: ${meta.format}, size: ${buf.length} bytes`);
      }
    }
  }
}

testSample().catch(console.error);

