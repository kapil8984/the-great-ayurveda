const https = require('https');

https.get('https://kottakkalaryavaidyasala.com/', { headers: { 'User-Agent': 'Mozilla/5.0' } }, res => {
  console.log('Status:', res.statusCode);
  console.log('Headers:', res.headers.location);
  let d = '';
  res.on('data', c => d += c);
  res.on('end', () => console.log('Body length:', d.length));
});

