const fs = require('fs');
const path = require('path');

const files = [
    'src/Ayurveda.Web/Views/Shared/_Footer.cshtml',
    'src/Ayurveda.Web/Areas/Admin/Views/Shared/_Layout.cshtml',
    'src/Ayurveda.Web/Controllers/HomeController.cs',
    'tests/e2e/test_group5_honest_content.js',
    'tests/e2e/test_group3_images_and_catalog.js'
];

files.forEach(f => {
    const src = path.resolve(__dirname, '../../', f);
    const dst = path.resolve(__dirname, 'output/backup', f);
    if (!fs.existsSync(dst)) {
        fs.mkdirSync(path.dirname(dst), { recursive: true });
        fs.copyFileSync(src, dst);
        console.log('Backed up:', f);
    } else {
        console.log('Already backed up:', f);
    }
});
