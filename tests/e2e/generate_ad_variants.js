const fs = require('fs');
const path = require('path');
const sharp = require('sharp');

const bannersDir = path.resolve(__dirname, '../../src/Ayurveda.Web/wwwroot/images/banners');

async function processAdVariants() {
  console.log('Generating 1600x400 desktop and 800x400 mobile WebP ad variants (< 120 KB)...');
  const files = fs.readdirSync(bannersDir).filter(f => f.startsWith('ad-') && f.endsWith('.webp') && !f.includes('-1600x400') && !f.includes('-800x400'));

  console.log(`Found ${files.length} base ad images.`);

  for (const file of files) {
    const baseName = file.replace(/\.webp$/, '');
    const srcPath = path.join(bannersDir, file);

    const desktopPath = path.join(bannersDir, `${baseName}-1600x400.webp`);
    const mobilePath = path.join(bannersDir, `${baseName}-800x400.webp`);

    // 1. Desktop: 1600x400 (< 120 KB)
    await sharp(srcPath)
      .resize(1600, 400, { fit: 'cover', position: 'center' })
      .webp({ quality: 80, effort: 5 })
      .toFile(desktopPath);

    let deskStat = fs.statSync(desktopPath);
    if (deskStat.size > 120 * 1024) {
      await sharp(srcPath)
        .resize(1600, 400, { fit: 'cover', position: 'center' })
        .webp({ quality: 65, effort: 6 })
        .toFile(desktopPath);
      deskStat = fs.statSync(desktopPath);
    }
    console.log(`[DESKTOP] ${baseName}-1600x400.webp -> ${(deskStat.size / 1024).toFixed(1)} KB`);

    // 2. Mobile: 800x400 (< 120 KB)
    await sharp(srcPath)
      .resize(800, 400, { fit: 'cover', position: 'center' })
      .webp({ quality: 80, effort: 5 })
      .toFile(mobilePath);

    let mobStat = fs.statSync(mobilePath);
    if (mobStat.size > 120 * 1024) {
      await sharp(srcPath)
        .resize(800, 400, { fit: 'cover', position: 'center' })
        .webp({ quality: 65, effort: 6 })
        .toFile(mobilePath);
      mobStat = fs.statSync(mobilePath);
    }
    console.log(`[MOBILE]  ${baseName}-800x400.webp -> ${(mobStat.size / 1024).toFixed(1)} KB`);
  }

  console.log('All ad variants created successfully.');
}

processAdVariants().catch(err => console.error(err));
