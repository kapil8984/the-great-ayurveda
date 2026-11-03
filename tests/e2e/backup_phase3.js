const fs = require('fs');
const path = require('path');

const root = path.resolve(__dirname, '../..');
const backupBase = path.join(root, 'tests/e2e/output/backup');

const files = [
  'src/Ayurveda.Infrastructure/Data/DbInitializer.cs',
  'src/Ayurveda.Application/ViewModels/YogaAndRemedyViewModels.cs',
  'src/Ayurveda.Web/Controllers/YogaController.cs',
  'src/Ayurveda.Web/Views/Yoga/Index.cshtml',
  'src/Ayurveda.Web/Views/Yoga/Details.cshtml',
  'src/Ayurveda.Web/Areas/Admin/Controllers/OtherAdminControllers.cs',
  'src/Ayurveda.Web/Areas/Admin/Views/Yoga/Index.cshtml'
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
console.log('Phase 3 backups complete.');
