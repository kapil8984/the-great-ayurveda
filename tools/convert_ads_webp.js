const fs = require('fs');
const path = require('path');
const sharp = require('d:/TheGreatAyurveda/The-Great-Ayurveda/tests/e2e/node_modules/sharp');

const brainDir = 'C:/Users/HP/.gemini/antigravity-ide/brain/d04381bf-47f5-49a6-9e7e-7e3d6a31e014';
const targetDir = 'd:/TheGreatAyurveda/The-Great-Ayurveda/src/Ayurveda.Web/wwwroot/images/banners';

const ads = [
    {
        srcPattern: /ad_ashwagandha_strip.*\.jpg$/,
        targetName: 'ad-ashwagandha-strip.webp',
        width: 1000,
        height: 380,
        placement: 'ad_home_strip'
    },
    {
        srcPattern: /ad_chyawanprash_strip.*\.jpg$/,
        targetName: 'ad-chyawanprash-strip.webp',
        width: 900,
        height: 342,
        placement: 'ad_home_strip'
    },
    {
        srcPattern: /ad_kumkumadi_side.*\.jpg$/,
        targetName: 'ad-kumkumadi-side.webp',
        width: 600,
        height: 600,
        placement: 'ad_shop_side'
    },
    {
        srcPattern: /ad_triphala_side.*\.jpg$/,
        targetName: 'ad-triphala-side.webp',
        width: 600,
        height: 600,
        placement: 'ad_shop_side'
    },
    {
        srcPattern: /ad_brahmi_inline.*\.jpg$/,
        targetName: 'ad-brahmi-inline.webp',
        width: 600,
        height: 450,
        placement: 'ad_shop_inline'
    },
    {
        srcPattern: /ad_ghee_inline.*\.jpg$/,
        targetName: 'ad-ghee-inline.webp',
        width: 600,
        height: 450,
        placement: 'ad_shop_inline'
    },
    {
        srcPattern: /ad_ojastea_product.*\.jpg$/,
        targetName: 'ad-ojastea-product.webp',
        width: 1000,
        height: 380,
        placement: 'ad_product_below'
    },
    {
        srcPattern: /ad_copper_bottle.*\.jpg$/,
        targetName: 'ad-copper-bottle.webp',
        width: 1000,
        height: 380,
        placement: 'ad_cart'
    }
];

async function main() {
    const brainFiles = fs.readdirSync(brainDir);

    for (const ad of ads) {
        const found = brainFiles.find(f => ad.srcPattern.test(f));
        if (!found) {
            console.error(`Source file for ${ad.targetName} not found!`);
            continue;
        }
        const fullSrc = path.join(brainDir, found);
        const fullDest = path.join(targetDir, ad.targetName);

        let quality = 80;
        let buf = await sharp(fullSrc)
            .resize(ad.width, ad.height, { fit: 'cover' })
            .webp({ quality, effort: 5 })
            .toBuffer();

        while (buf.length > 48 * 1024 && quality > 40) {
            quality -= 5;
            buf = await sharp(fullSrc)
                .resize(ad.width, ad.height, { fit: 'cover' })
                .webp({ quality, effort: 5 })
                .toBuffer();
        }

        fs.writeFileSync(fullDest, buf);
        console.log(`Saved ${ad.targetName}: ${(buf.length / 1024).toFixed(1)} KB (quality: ${quality})`);
    }
}

main().catch(err => {
    console.error(err);
    process.exit(1);
});
