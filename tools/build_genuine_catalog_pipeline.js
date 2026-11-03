const fs = require('fs');
const path = require('path');
const https = require('https');
const http = require('http');
const sharp = require('../tests/e2e/node_modules/sharp');

const CATALOG_PATH = path.join(__dirname, '..', 'src', 'Ayurveda.Infrastructure', 'Data', 'catalog_300plus.json');
const WEB_PRODUCTS_DIR = path.join(__dirname, '..', 'src', 'Ayurveda.Web', 'wwwroot', 'images', 'products');
const ADMIN_PRODUCTS_DIR = path.join(__dirname, '..', 'src', 'Ayurveda.Admin', 'wwwroot', 'images', 'products');
const BACKUP_OLD_DIR = path.join(__dirname, '..', 'scratch', 'old_ai_images');

const catalog = JSON.parse(fs.readFileSync(CATALOG_PATH, 'utf-8'));

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

function cleanTitle(brand, title) {
  let clean = title;
  clean = clean.replace(new RegExp(`^${brand}\\s*`, 'i'), '');
  clean = clean.replace(/^(Himalaya|Dabur|Patanjali|Baidyanath|Zandu|Charak|AVP|Sri Sri Tattva|Sri Sri|Maharishi Ayurveda|Kottakkal)\s*/i, '');
  clean = clean.replace(/\([^)]*\)/g, '');
  clean = clean.replace(/\//g, ' ');
  clean = clean.replace(/\s+/g, ' ').trim();
  return clean;
}

const BRAND_CONFIGS = {
  'Himalaya Wellness': {
    brandFolder: 'himalaya',
    type: 'shopify',
    hosts: ['himalayawellness.in']
  },
  'Dabur India': {
    brandFolder: 'dabur',
    type: 'shopify',
    hosts: ['www.daburshop.com']
  },
  'Sri Sri Tattva': {
    brandFolder: 'sri-sri-tattva',
    type: 'shopify',
    hosts: ['www.srisritattva.com']
  },
  'Zandu Care': {
    brandFolder: 'zandu',
    type: 'shopify',
    hosts: ['zanducare.com']
  },
  'Baidyanath': {
    brandFolder: 'baidyanath',
    type: 'shopify',
    hosts: ['baidyanath.co.in', 'baidyanathayurved.com']
  },
  'Charak Pharma': {
    brandFolder: 'charak',
    type: 'shopify',
    hosts: ['charak.com']
  },
  'Maharishi Ayurveda': {
    brandFolder: 'maharishi',
    type: 'shopify',
    hosts: ['maharishiayurvedaindia.com']
  },
  'AVP': {
    brandFolder: 'avp',
    type: 'shopify',
    hosts: ['avpayurveda.com']
  },
  'Kottakkal Arya Vaidya Sala': {
    brandFolder: 'kottakkal',
    type: 'shopify',
    hosts: ['www.ayurkart.com']
  },
  'Patanjali Ayurved': {
    brandFolder: 'patanjali',
    type: 'patanjali',
    hosts: ['www.patanjaliayurved.net']
  }
};

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
          description: fullRes.json.description || p.body,
          sourceHost: host,
          sourceUrl: `https://${host}${cleanProdUrl}`
        };
      } else if (p.image) {
        return {
          title: p.title,
          images: [p.image],
          description: p.body,
          sourceHost: host,
          sourceUrl: `https://${host}${cleanProdUrl}`
        };
      }
    }
  }
  return null;
}

async function queryPatanjali(query) {
  const searchUrl = `https://www.patanjaliayurved.net/search/${encodeURIComponent(query)}`;
  const res = await fetch(searchUrl, {}, false);
  if (!res.buffer) return null;
  const html = res.buffer.toString('utf-8');
  const imgMatches = [...html.matchAll(/src=["']([^"']*(?:product_images\/400x500|product_images\/400x400)[^"']+)["']/gi)].map(m => m[1]);
  if (imgMatches.length > 0) {
    // Unique images
    const unique = [...new Set(imgMatches)].slice(0, 5);
    return {
      title: query,
      images: unique,
      description: '',
      sourceHost: 'www.patanjaliayurved.net',
      sourceUrl: searchUrl
    };
  }
  return null;
}

async function main() {
  console.log('========================================================================');
  console.log(' THE GREAT AYURVEDA - GENUINE PRODUCT PHOTO INGESTION PIPELINE');
  console.log('========================================================================\n');

  if (!fs.existsSync(BACKUP_OLD_DIR)) fs.mkdirSync(BACKUP_OLD_DIR, { recursive: true });

  // 1. Move all existing flat AI-generated images to scratch/old_ai_images
  console.log('[1/5] Removing existing flat AI-generated product images...');
  const oldFiles = fs.readdirSync(WEB_PRODUCTS_DIR).filter(f => f.endsWith('.webp') || f.endsWith('.svg') || f.endsWith('.png'));
  console.log(`Found ${oldFiles.length} legacy product images in ${WEB_PRODUCTS_DIR}`);
  let removedCount = 0;
  for (const f of oldFiles) {
    const srcPath = path.join(WEB_PRODUCTS_DIR, f);
    const destPath = path.join(BACKUP_OLD_DIR, f);
    fs.renameSync(srcPath, destPath);
    removedCount++;
  }
  console.log(`Archived and removed ${removedCount} AI-generated images.\n`);

  // 2. Prepare brand folders
  console.log('[2/5] Creating brand directories...');
  for (const cfg of Object.values(BRAND_CONFIGS)) {
    const webDir = path.join(WEB_PRODUCTS_DIR, cfg.brandFolder);
    const adminDir = path.join(ADMIN_PRODUCTS_DIR, cfg.brandFolder);
    if (!fs.existsSync(webDir)) fs.mkdirSync(webDir, { recursive: true });
    if (!fs.existsSync(adminDir)) fs.mkdirSync(adminDir, { recursive: true });
  }

  // 3. Process products
  console.log('[3/5] Querying genuine photographs for 400 catalog products...\n');

  const stats = {
    totalChecked: catalog.length,
    genuineFound: 0,
    multipleAnglesCount: 0,
    singleAngleCount: 0,
    unavailableCount: 0,
    totalImagesSaved: 0,
    byBrand: {},
    unavailableProducts: []
  };

  for (let i = 0; i < catalog.length; i++) {
    const p = catalog[i];
    const brandName = p.brand;
    const cfg = BRAND_CONFIGS[brandName];

    if (!stats.byBrand[brandName]) {
      stats.byBrand[brandName] = { total: 0, found: 0, multipleAngles: 0, singleAngle: 0, totalPhotos: 0 };
    }
    stats.byBrand[brandName].total++;

    const cleaned = cleanTitle(brandName, p.title);
    const slug = slugify(`${brandName}-${cleaned}`);

    let match = null;
    if (cfg) {
      if (cfg.type === 'shopify') {
        match = await queryShopify(cfg.hosts, cleaned);
        if (!match && cleaned.includes(' ')) {
          // Try first 2 words if multi-word
          const shortQuery = cleaned.split(' ').slice(0, 2).join(' ');
          match = await queryShopify(cfg.hosts, shortQuery);
        }
      } else if (cfg.type === 'patanjali') {
        match = await queryPatanjali(cleaned);
        if (!match && cleaned.includes(' ')) {
          const shortQuery = cleaned.split(' ').slice(0, 2).join(' ');
          match = await queryPatanjali(shortQuery);
        }
      }
    }

    // Secondary fallback: if still not found, check Ayurkart for classical/ayurvedic formulation
    if (!match && brandName !== 'Kottakkal Arya Vaidya Sala') {
      match = await queryShopify(['www.ayurkart.com'], `${brandName} ${cleaned}`);
    }

    if (match && match.images && match.images.length > 0) {
      stats.genuineFound++;
      stats.byBrand[brandName].found++;

      const imagesSavedForProduct = [];
      const anglesLimit = Math.min(match.images.length, 5); // Up to 5 genuine angles (Front, Back, Side, Nutritional, Box)

      for (let angleIdx = 0; angleIdx < anglesLimit; angleIdx++) {
        let imgUrl = match.images[angleIdx];
        if (imgUrl.startsWith('//')) imgUrl = 'https:' + imgUrl;
        
        try {
          const bufRes = await fetch(imgUrl, {}, false);
          if (bufRes.buffer && bufRes.buffer.length > 1000) {
            // Process with sharp: preserve aspect ratio, fit inside 1000x1000, WebP
            const filename = `${slug}-${angleIdx + 1}.webp`;
            const webSavePath = path.join(WEB_PRODUCTS_DIR, cfg.brandFolder, filename);
            const adminSavePath = path.join(ADMIN_PRODUCTS_DIR, cfg.brandFolder, filename);

            await sharp(bufRes.buffer)
              .resize(1000, 1000, {
                fit: 'inside',
                withoutEnlargement: true
              })
              .webp({ quality: 85 })
              .toFile(webSavePath);

            fs.copyFileSync(webSavePath, adminSavePath);

            const relUrl = `/images/products/${cfg.brandFolder}/${filename}`;
            imagesSavedForProduct.push(relUrl);
            stats.totalImagesSaved++;
            stats.byBrand[brandName].totalPhotos++;
          }
        } catch (imgErr) {
          console.error(`     Error processing image ${angleIdx + 1} for ${p.title}:`, imgErr.message);
        }
      }

      if (imagesSavedForProduct.length > 0) {
        p.imageUrl = imagesSavedForProduct[0];
        p.additionalImages = imagesSavedForProduct.slice(1);

        if (imagesSavedForProduct.length > 1) {
          stats.multipleAnglesCount++;
          stats.byBrand[brandName].multipleAngles++;
        } else {
          stats.singleAngleCount++;
          stats.byBrand[brandName].singleAngle++;
        }

        console.log(`[${i + 1}/400] ✅ [${brandName}] ${p.title} -> ${imagesSavedForProduct.length} genuine angle(s) saved.`);
      } else {
        // Fallback placeholder if image download failed
        p.imageUrl = '/images/placeholder.svg';
        p.additionalImages = [];
        stats.unavailableCount++;
        stats.unavailableProducts.push({ id: p.id, brand: brandName, title: p.title });
        console.log(`[${i + 1}/400] ⚠️ [${brandName}] ${p.title} -> Download failed, marked unavailable.`);
      }
    } else {
      stats.unavailableCount++;
      stats.unavailableProducts.push({ id: p.id, brand: brandName, title: p.title });
      p.imageUrl = '/images/placeholder.svg';
      p.additionalImages = [];
      console.log(`[${i + 1}/400] ❌ [${brandName}] ${p.title} -> Genuine photo not found on manufacturer channels.`);
    }

    // Small delay to be polite to host servers
    await sleep(80);
  }

  // 4. Write updated catalog_300plus.json
  console.log('\n[4/5] Saving updated catalog_300plus.json...');
  fs.writeFileSync(CATALOG_PATH, JSON.stringify(catalog, null, 2), 'utf-8');

  // Also write audit report
  const auditPath = path.join(__dirname, '..', 'catalog_genuine_audit.json');
  fs.writeFileSync(auditPath, JSON.stringify(stats, null, 2), 'utf-8');

  console.log('\n========================================================================');
  console.log(' AUDIT & INGESTION COMPLETE');
  console.log(` Total Products Checked:          ${stats.totalChecked}`);
  console.log(` Total Products with Genuine Photos: ${stats.genuineFound}`);
  console.log(` Products with Multiple Angles:   ${stats.multipleAnglesCount}`);
  console.log(` Products with Single Photo:      ${stats.singleAngleCount}`);
  console.log(` Products Unavailable:            ${stats.unavailableCount}`);
  console.log(` Total Genuine Photos Saved:      ${stats.totalImagesSaved}`);
  console.log(` Legacy AI Images Removed:        ${removedCount}`);
  console.log('========================================================================\n');
}

main().catch(console.error);

