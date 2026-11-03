const https = require('https');
const sharp = require('../tests/e2e/node_modules/sharp');

function fetch(url) {
  return new Promise((resolve) => {
    https.get(url, {
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36'
      }
    }, res => {
      let chunks = [];
      res.on('data', c => chunks.push(c));
      res.on('end', () => resolve(Buffer.concat(chunks)));
    }).on('error', e => resolve(null));
  });
}

async function testPatanjali() {
  const imgUrl = 'https://www.patanjaliayurved.net/assets/product_images/400x500/1739858231DantKantiNatural100g-1.webp';
  const buf = await fetch(imgUrl);
  if (buf) {
    const meta = await sharp(buf).metadata();
    console.log('Patanjali genuine photo downloaded:', meta.width, 'x', meta.height, meta.format, buf.length, 'bytes');
  }
}

testPatanjali();

