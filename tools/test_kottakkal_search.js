const https = require('https');

function fetchJson(url) {
  return new Promise((resolve, reject) => {
    https.get(url, {
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko)',
        'Accept': 'application/json, text/plain, */*'
      }
    }, res => {
      let d = '';
      res.on('data', c => d += c);
      res.on('end', () => {
        try {
          resolve({ status: res.statusCode, json: JSON.parse(d) });
        } catch (e) {
          resolve({ status: res.statusCode, text: d.slice(0, 200) });
        }
      });
    }).on('error', reject);
  });
}

async function run() {
  console.log('Testing AyurCentral:');
  const ac = await fetchJson('https://ayurcentralonline.com/search/suggest.json?q=Kottakkal+Arya+Vaidya+Sala&resources[type]=product');
  console.log('AyurCentral suggest:', ac.status, ac.json ? ac.json.resources.results.products.length : ac.text);

  console.log('\nTesting Ayurkart:');
  const ak = await fetchJson('https://www.ayurkart.com/search/suggest.json?q=Kottakkal&resources[type]=product');
  console.log('Ayurkart suggest:', ak.status, ak.json ? ak.json.resources.results.products.length : ak.text);
}

run();

