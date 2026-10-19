const sharp = require('sharp');
const fs = require('fs');
const path = require('path');

const brainDir = "C:\\Users\\HP\\.gemini\\antigravity-ide\\brain\\d04381bf-47f5-49a6-9e7e-7e3d6a31e014";
const targetDir = path.resolve(__dirname, "../../src/Ayurveda.Web/wwwroot/images/banners");

const mappings = [
    { src: "hero_diwali_m_1792411005857.jpg", dest: "banner-hero-diwali-m.webp" },
    { src: "hero_dhanteras_m_1792411026165.jpg", dest: "banner-hero-dhanteras-m.webp" },
    { src: "hero_navratri_m_1792411043818.jpg", dest: "banner-hero-navratri-m.webp" },
    { src: "hero_winter_m_1792411063504.jpg", dest: "banner-hero-winter-m.webp" },
    { src: "hero_teayoga_m_1792411080339.jpg", dest: "banner-hero-teayoga-m.webp" },
    { src: "hero_consult_m_1792411098146.jpg", dest: "banner-hero-consult-m.webp" },
    { src: "mid_dinacharya_m_1792411118558.jpg", dest: "banner-mid-dinacharya-m.webp" },
    { src: "mid_herbs_m_1792411138091.jpg", dest: "banner-mid-herbs-m.webp" },
    { src: "mid_panchakarma_m_1792411161625.jpg", dest: "banner-mid-panchakarma-m.webp" },
    { src: "mid_skinrituals_m_1792411196850.jpg", dest: "banner-mid-skinrituals-m.webp" }
];

async function convert() {
    console.log("Converting mobile banner images to 800x1000 WebP (< 60 KB)...");
    for (const m of mappings) {
        const inputPath = path.join(brainDir, m.src);
        const outputPath = path.join(targetDir, m.dest);
        
        let quality = 75;
        let buffer;
        while (quality >= 30) {
            buffer = await sharp(inputPath)
                .resize(800, 1000, { fit: 'cover', position: 'center' })
                .webp({ quality, effort: 6 })
                .toBuffer();
            
            if (buffer.length < 60 * 1024) {
                break;
            }
            quality -= 5;
        }
        
        fs.writeFileSync(outputPath, buffer);
        const meta = await sharp(outputPath).metadata();
        console.log(`[OK] ${m.dest}: ${meta.width}x${meta.height}, ${Math.round(buffer.length / 1024)} KB (quality=${quality})`);
    }
    console.log("All 10 mobile banners successfully converted and saved!");
}

convert().catch(err => {
    console.error(err);
    process.exit(1);
});
