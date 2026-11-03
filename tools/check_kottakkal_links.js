const https = require('https');

https.get('https://kottakkalaryavaidyasala.com/', { headers: { 'User-Agent': 'Mozilla/5.0' } }, res => {
  let d = '';
  res.on('data', c => d += c);
  res.on('end', () => {
    const imgMatches = [...d.matchAll(/<img[^>]+src=["']([^"']*(?:product|upload|wp-content)[^"']*)["']/gi)].map(m => m[1]);
    console.log('Sample Kottakkal product images:', [...new Set(imgMatches)].slice(0, 10));
    const prodLinks = [...d.matchAll(/href=["']([^"']*(?:product|shop)[^"']*)["']/gi)].map(m => m[1]);
    console.log('Sample Kottakkal product links:', [...new Set(prodLinks)].slice(0, 10));
  });
});

