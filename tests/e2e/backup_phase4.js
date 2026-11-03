const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '../..');
const backupBase = path.join(root, 'tests/e2e/output/backup');

const files = [
  'src/Ayurveda.Web/Views/Shared/_AdBanner.cshtml',
  'src/Ayurveda.Web/wwwroot/js/site.js',
  'src/Ayurveda.Web/Views/Shared/_Layout.cshtml',
  'src/Ayurveda.Web/appsettings.json'
];

for (const rel of files) {
  const src = path.join(root, rel);
  const dest = path.join(backupBase, rel);
  const destDir = path.dirname(dest);
  if (!fs.existsSync(destDir)) {
    fs.mkdirSync(destDir, { recursive: true });
  }
  if (!fs.existsSync(dest)) {
    fs.copyFileSync(src, dest);
    console.log(`Backed up: ${rel} -> ${dest}`);
  } else {
    console.log(`Backup already exists: ${dest}`);
  }
}
console.log('Phase 4 backups complete.');
