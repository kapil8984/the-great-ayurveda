const fs = require('fs');
const path = require('path');
const sharp = require('../tests/e2e/node_modules/sharp');

const brainDir = 'C:/Users/HP/.gemini/antigravity-ide/brain/4e2d7980-3744-4de0-bb20-91628ef25197';
const doctorsDir = path.join(__dirname, '..', 'src', 'Ayurveda.Web', 'wwwroot', 'images', 'doctors');

const doctorMap = [
    { num: 14, pattern: /^doc_somnath_sen.*\.jpg$/ },
    { num: 15, pattern: /^doc_priyamvada_rao.*\.jpg$/ },
    { num: 16, pattern: /^doc_madhavan_pillai.*\.jpg$/ },
    { num: 17, pattern: /^doc_alok_tiwari.*\.jpg$/ },
    { num: 18, pattern: /^doc_rituja_chawla.*\.jpg$/ },
    { num: 19, pattern: /^doc_eknath_gaikwad.*\.jpg$/ },
    { num: 20, pattern: /^doc_jayashree_sundaram.*\.jpg$/ },
    { num: 21, pattern: /^doc_gaurav_aggarwal.*\.jpg$/ },
    { num: 22, pattern: /^doc_vasantha_kumari.*\.jpg$/ },
    { num: 23, pattern: /^doc_nikhilesh_roy.*\.jpg$/ },
    { num: 24, pattern: /^doc_sarojini_nair.*\.jpg$/ }
];

async function main() {
    const files = fs.readdirSync(brainDir);

    for (const d of doctorMap) {
        const match = files.find(f => d.pattern.test(f));
        if (!match) {
            console.error(`Missing generated image for Doctor ${d.num}`);
            continue;
        }

        const srcPath = path.join(brainDir, match);
        const destPath = path.join(doctorsDir, `doctor-${d.num}.webp`);

        // Target: 400x400 WebP between 20 KB and 40 KB
        let quality = 80;
        let buf = await sharp(srcPath)
            .resize(400, 400, { fit: 'cover', position: 'top' })
            .webp({ quality })
            .toBuffer();

        while (buf.length > 39 * 1024 && quality > 40) {
            quality -= 4;
            buf = await sharp(srcPath)
                .resize(400, 400, { fit: 'cover', position: 'top' })
                .webp({ quality })
                .toBuffer();
        }

        while (buf.length < 20 * 1024 && quality < 95) {
            quality += 4;
            buf = await sharp(srcPath)
                .resize(400, 400, { fit: 'cover', position: 'top' })
                .webp({ quality })
                .toBuffer();
        }

        fs.writeFileSync(destPath, buf);
        console.log(`Doctor ${d.num} -> ${destPath}: ${(buf.length / 1024).toFixed(1)} KB (quality: ${quality})`);
    }
}

main().catch(err => {
    console.error(err);
    process.exit(1);
});
