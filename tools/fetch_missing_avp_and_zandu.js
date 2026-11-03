const fs = require('fs');
const path = require('path');
const https = require('https');
const http = require('http');
const sharp = require('../tests/e2e/node_modules/sharp');

const CATALOG_PATH = path.join(__dirname, '..', 'src', 'Ayurveda.Infrastructure', 'Data', 'catalog_300plus.json');
const WEB_PRODUCTS_DIR = path.join(__dirname, '..', 'src', 'Ayurveda.Web', 'wwwroot', 'images', 'products');
const ADMIN_PRODUCTS_DIR = path.join(__dirname, '..', 'src', 'Ayurveda.Admin', 'wwwroot', 'images', 'products');
const AUDIT_PATH = path.join(__dirname, '..', 'catalog_genuine_audit.json');

const catalog = JSON.parse(fs.readFileSync(CATALOG_PATH, 'utf-8'));
const audit = JSON.parse(fs.readFileSync(AUDIT_PATH, 'utf-8'));

function sleep(ms) {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function slugify(text) {
  return text.toString().toLowerCase()
    .replace(/[^\w\s-]/g, '')
    .replace(/[\s_-]+/g, '-')
    .replace(/^-+|-+$/g, '');
}

function fetch(url, headers = {}, isJson = true) {
  return new Promise((resolve) => {
    if (url.startsWith('//')) url = 'https:' + url;
    const parsed = new URL(url);
    const client = parsed.protocol === 'https:' ? https : http;
    const req = client.get(parsed, {
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
        'Accept': isJson ? 'application/json, text/plain, */*' : '*/*',
        ...headers
      }
    }, res => {
      if ([301, 302, 307, 308].includes(res.statusCode) && res.headers.location) {
        return resolve(fetch(new URL(res.headers.location, url).href, headers, isJson));
      }
      let chunks = [];
      res.on('data', c => chunks.push(c));
      res.on('end', () => {
        const bodyBuf = Buffer.concat(chunks);
        if (!isJson) return resolve({ status: res.statusCode, buffer: bodyBuf });
        try {
          const json = JSON.parse(bodyBuf.toString('utf-8'));
          resolve({ status: res.statusCode, json });
        } catch (e) {
          resolve({ status: res.statusCode, text: bodyBuf.toString('utf-8').slice(0, 300) });
        }
      });
    });
    req.on('error', e => resolve({ error: e.message }));
    req.setTimeout(15000, () => { req.destroy(); resolve({ error: 'Timeout' }); });
  });
}

async function queryShopify(hosts, query) {
  for (const host of hosts) {
    const searchUrl = `https://${host}/search/suggest.json?q=${encodeURIComponent(query)}&resources[type]=product`;
    const res = await fetch(searchUrl);
    if (res.json && res.json.resources && res.json.resources.results.products && res.json.resources.results.products.length > 0) {
      const p = res.json.resources.results.products[0];
      const cleanProdUrl = p.url.split('?')[0];
      const fullRes = await fetch(`https://${host}${cleanProdUrl}.js`);
      if (fullRes.json && fullRes.json.images && fullRes.json.images.length > 0) {
        return {
          title: fullRes.json.title || p.title,
          images: fullRes.json.images,
          sourceHost: host
        };
      } else if (p.image) {
        return {
          title: p.title,
          images: [p.image],
          sourceHost: host
        };
      }
    }
  }
  return null;
}

async function run() {
  console.log('Fetching genuine photos for AVP and missed products...\n');

  // Ensure avp and zandu folders exist
  const avpWeb = path.join(WEB_PRODUCTS_DIR, 'avp');
  const avpAdmin = path.join(ADMIN_PRODUCTS_DIR, 'avp');
  if (!fs.existsSync(avpWeb)) fs.mkdirSync(avpWeb, { recursive: true });
  if (!fs.existsSync(avpAdmin)) fs.mkdirSync(avpAdmin, { recursive: true });

  const targets = catalog.filter(p => 
    p.brand === 'AVP — The Arya Vaidya Pharmacy' ||
    (p.imageUrl && p.imageUrl.includes('placeholder'))
  );

  console.log(`Processing ${targets.length} targets...`);

  let addedPhotos = 0;
  for (let i = 0; i < targets.length; i++) {
    const p = targets[i];
    let brandFolder = 'avp';
    let hosts = ['avpayurveda.com', 'www.ayurkart.com'];

    if (p.brand === 'Zandu Care') {
      brandFolder = 'zandu';
      hosts = ['zanducare.com', 'www.ayurkart.com'];
    } else if (p.brand === 'Dabur India') {
      brandFolder = 'dabur';
      hosts = ['www.daburshop.com', 'www.ayurkart.com'];
    } else if (p.brand === 'Kottakkal Arya Vaidya Sala') {
      brandFolder = 'kottakkal';
      hosts = ['www.ayurkart.com'];
    }

    // Clean title for search
    let q = p.title;
    q = q.replace(/^AVP\s*/i, '');
    q = q.replace(/^Zandu\s*/i, '');
    q = q.replace(/^Dabur\s*/i, '');
    q = q.replace(/\([^)]*\)/g, '');
    q = q.replace(/\b(Soft Gel Capsules|Capsules|Drops|Syrup|Tablet|Tablets|Churna|Tailam|Oil)\b/gi, '');
    q = q.trim();

    let match = await queryShopify(hosts, q);
    if (!match && q.includes(' ')) {
      const firstWord = q.split(' ')[0];
      match = await queryShopify(hosts, firstWord);
    }
    if (!match) {
      match = await queryShopify(['www.ayurkart.com'], `${p.brand.split(' ')[0]} ${q}`);
    }

    if (match && match.images && match.images.length > 0) {
      const slug = slugify(`${p.brand}-${p.title}`);
      const imagesSaved = [];
      const count = Math.min(match.images.length, 5);

      for (let idx = 0; idx < count; idx++) {
        let imgUrl = match.images[idx];
        if (imgUrl.startsWith('//')) imgUrl = 'https:' + imgUrl;
        try {
          const bufRes = await fetch(imgUrl, {}, false);
          if (bufRes.buffer && bufRes.buffer.length > 1000) {
            const filename = `${slug}-${idx + 1}.webp`;
            const webSavePath = path.join(WEB_PRODUCTS_DIR, brandFolder, filename);
            const adminSavePath = path.join(ADMIN_PRODUCTS_DIR, brandFolder, filename);

            await sharp(bufRes.buffer)
              .resize(1000, 1000, { fit: 'inside', withoutEnlargement: true })
              .webp({ quality: 85 })
              .toFile(webSavePath);

            fs.copyFileSync(webSavePath, adminSavePath);
            imagesSaved.push(`/images/products/${brandFolder}/${filename}`);
            addedPhotos++;
          }
        } catch (e) {
          console.error(`Error saving image for ${p.title}:`, e.message);
        }
      }

      if (imagesSaved.length > 0) {
        p.imageUrl = imagesSaved[0];
        p.additionalImages = imagesSaved.slice(1);
        console.log(`[${i + 1}/${targets.length}] ✅ [${p.brand}] ${p.title} -> ${imagesSaved.length} genuine angle(s) saved.`);
      }
    } else {
      console.log(`[${i + 1}/${targets.length}] ❌ [${p.brand}] ${p.title} -> still not found, preserved clean placeholder.`);
    }

    await sleep(60);
  }

  // Save catalog
  fs.writeFileSync(CATALOG_PATH, JSON.stringify(catalog, null, 2), 'utf-8');
  console.log(`\nComplete! Added ${addedPhotos} new genuine photo angles.`);
}

run().catch(console.error);

