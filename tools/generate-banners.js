const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const OUTPUT_DIR = path.resolve(__dirname, '..', 'src', 'Ayurveda.Web', 'wwwroot', 'images', 'banners');

if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

const BANNERS = [
    // 6 HERO BANNERS
    {
        filename: 'banner-hero-diwali.webp',
        title: 'Diwali Ojas Mahotsav',
        tagline: 'FESTIVE AROGYA CELEBRATION',
        desc: 'Divine Radiance & Rejuvenation — Flat 30% Off on Pure Classical Formulations & Gold Rasayanas',
        cta: 'Shop Festive Offers',
        code: 'Use Code: DIWALI30',
        bgGradient: 'linear-gradient(135deg, #1A0D00 0%, #3D1E04 35%, #1F5E3B 80%, #0d2819 100%)',
        accentColor: '#F7C85C',
        secondaryColor: '#E3A72F',
        theme: 'diwali'
    },
    {
        filename: 'banner-hero-navratri.webp',
        title: 'Navratri Sattvic Shuddhi',
        tagline: 'SACRED FASTING & DIGESTIVE DETOX',
        desc: 'Kindle Digestive Agni & Pacify Pitta with Classical Churnas, Organic Vrat Ghee & Herbal Teas',
        cta: 'Explore Sattvic Blends',
        code: 'Use Code: NAVRATRI20',
        bgGradient: 'linear-gradient(135deg, #1C0A1E 0%, #35153E 35%, #143F27 75%, #0B2416 100%)',
        accentColor: '#FF9E64',
        secondaryColor: '#F7C85C',
        theme: 'navratri'
    },
    {
        filename: 'banner-hero-dhanteras.webp',
        title: 'Dhanteras Arogya & Gift Hampers',
        tagline: 'THE SACRED GIFT OF HEALTH & LONGEVITY',
        desc: 'Bless Loved Ones with Authentic Wooden Chests, Pure Gir Cow Bilona Ghee & Shilajit Elixirs',
        cta: 'View Royal Hampers',
        code: 'Complimentary Gold Diya Included',
        bgGradient: 'linear-gradient(135deg, #241400 0%, #4A2B00 35%, #1F5E3B 75%, #122B1C 100%)',
        accentColor: '#F7D070',
        secondaryColor: '#E3A72F',
        theme: 'dhanteras'
    },
    {
        filename: 'banner-hero-winter.webp',
        title: 'Winter Immunity Shield',
        tagline: 'SEASONAL RITUCHARYA CARE',
        desc: 'Pacify Vata & Fortify Lungs — Wild Forest Amla Chyawanprash, Ginger Kadhas & Warm Body Oils',
        cta: 'Strengthen Immunity',
        code: 'Flat 20% Off On Seasonal Formulations',
        bgGradient: 'linear-gradient(135deg, #091D28 0%, #103348 35%, #1B5E3C 75%, #0E291B 100%)',
        accentColor: '#6DD5ED',
        secondaryColor: '#F7C85C',
        theme: 'winter'
    },
    {
        filename: 'banner-hero-consult.webp',
        title: 'Free First Vaidya Consultation',
        tagline: '1-ON-1 ONLINE CLINICAL ASSESSMENT',
        desc: 'Discover Your Unique Prakriti & Imbalances with Senior BAMS Doctors via Private Video Call',
        cta: 'Book Free Consultation',
        code: '100% Free • Personalized Diet Chart',
        bgGradient: 'linear-gradient(135deg, #092617 0%, #154D2F 40%, #2D7A4F 80%, #143823 100%)',
        accentColor: '#98EE99',
        secondaryColor: '#F7C85C',
        theme: 'consult'
    },
    {
        filename: 'banner-hero-teayoga.webp',
        title: 'Sacred Herbal Teas & Yoga Harmony',
        tagline: 'PRANA & MIND WELLNESS',
        desc: 'Infuse Your Sadhana with Handcrafted Whole-Leaf Botanical Brews, Tulsi Kadhas & Organic Cotton Mats',
        cta: 'Explore Wellness Collection',
        code: 'Special Bundles Available',
        bgGradient: 'linear-gradient(135deg, #1B1E12 0%, #363C21 35%, #1F5E3B 75%, #0C2B19 100%)',
        accentColor: '#D4E157',
        secondaryColor: '#F7C85C',
        theme: 'yoga'
    },

    // 4 MIDDLE-OF-PAGE BANNERS
    {
        filename: 'banner-mid-panchakarma.webp',
        title: 'Authentic Panchakarma Home Kits',
        tagline: 'SHODHANA & REJUVENATION THERAPY',
        desc: 'Ancient Detox Tools: Pure Copper Neti Pots, Bronze Kansa Vatki Bowls, Anu Taila & Massage Tailas',
        cta: 'Shop Panchakarma',
        code: 'AFI Standards • Classical Herbs',
        bgGradient: 'linear-gradient(135deg, #1B261D 0%, #2A4031 40%, #154228 80%, #0F2A1B 100%)',
        accentColor: '#A3D9A5',
        secondaryColor: '#E3A72F',
        theme: 'panchakarma'
    },
    {
        filename: 'banner-mid-herbs.webp',
        title: 'Himalayan Wild Harvest & Raw Herbs',
        tagline: 'SINGLE-ORIGIN PURE BOTANICALS',
        desc: 'Wild-Crafted Grade-A Shilajit, Ashwagandha Roots, Shatavari & Giloy Sourced Directly from the Himalayas',
        cta: 'Explore Raw Botanicals',
        code: '100% Organically Foraged',
        bgGradient: 'linear-gradient(135deg, #1E1710 0%, #3D2D1E 40%, #1E462E 80%, #11291B 100%)',
        accentColor: '#F5BE75',
        secondaryColor: '#E3A72F',
        theme: 'herbs'
    },
    {
        filename: 'banner-mid-skinrituals.webp',
        title: 'Royal Kumkumadi & Ubtan Glow',
        tagline: 'TIMELESS AYURVEDIC BEAUTY TRADITIONS',
        desc: 'Infused with Kashmiri Saffron, Sandalwood & 24 Classical Herbs for Natural Cellular Radiance',
        cta: 'Discover Skin Elixirs',
        code: 'Traditional Ayurvedic Formulations',
        bgGradient: 'linear-gradient(135deg, #2D1414 0%, #4D2121 40%, #2E5339 80%, #132E1F 100%)',
        accentColor: '#FFAAA6',
        secondaryColor: '#F7C85C',
        theme: 'skin'
    },
    {
        filename: 'banner-mid-dinacharya.webp',
        title: 'Daily Dinacharya Circadian Rituals',
        tagline: 'SACRED MORNING & EVENING HABITS',
        desc: 'Oil Pulling, Pure Copper Scrapers, Nasya Drops & Restful Brahmi Infusions for Lifelong Vitality',
        cta: 'Learn Dinacharya Routine',
        code: 'Ancient Daily Health Blueprint',
        bgGradient: 'linear-gradient(135deg, #12211C 0%, #1E3830 40%, #27563D 80%, #0F261B 100%)',
        accentColor: '#80DEEA',
        secondaryColor: '#F7C85C',
        theme: 'dinacharya'
    }
];

function generateHtml(b) {
    return `<!DOCTYPE html>
<html>
<head>
<meta charset="utf-8">
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body {
    width: 1920px;
    height: 640px;
    background: ${b.bgGradient};
    color: #FFFDF9;
    font-family: 'Plus Jakarta Sans', system-ui, -apple-system, sans-serif;
    position: relative;
    overflow: hidden;
    display: flex;
    align-items: center;
  }
  /* Decorative Mandalas & Elements */
  .bg-glow {
    position: absolute;
    width: 900px;
    height: 900px;
    right: -100px;
    top: -130px;
    background: radial-gradient(circle, ${b.accentColor}25 0%, transparent 70%);
    border-radius: 50%;
  }
  .bg-glow-left {
    position: absolute;
    width: 600px;
    height: 600px;
    left: -150px;
    bottom: -150px;
    background: radial-gradient(circle, #E3A72F18 0%, transparent 70%);
    border-radius: 50%;
  }
  .mandala-svg {
    position: absolute;
    right: 80px;
    top: 50%;
    transform: translateY(-50%);
    width: 520px;
    height: 520px;
    opacity: 0.18;
  }
  .container {
    width: 100%;
    max-width: 1680px;
    margin: 0 auto;
    padding: 0 100px;
    position: relative;
    z-index: 2;
    display: flex;
    justify-content: space-between;
    align-items: center;
  }
  .content {
    max-width: 920px;
  }
  .tagline-badge {
    display: inline-flex;
    align-items: center;
    gap: 8px;
    background: rgba(255, 255, 255, 0.1);
    border: 1px solid ${b.accentColor}66;
    color: ${b.accentColor};
    font-size: 15px;
    font-weight: 700;
    letter-spacing: 0.12em;
    padding: 8px 18px;
    border-radius: 9999px;
    margin-bottom: 24px;
    backdrop-filter: blur(8px);
  }
  .title {
    font-family: 'Cormorant Garamond', Georgia, serif;
    font-size: 64px;
    font-weight: 700;
    line-height: 1.1;
    margin-bottom: 20px;
    color: #FFFFFF;
    text-shadow: 0 4px 18px rgba(0, 0, 0, 0.4);
    letter-spacing: -0.01em;
  }
  .title span {
    color: ${b.accentColor};
  }
  .desc {
    font-size: 20px;
    line-height: 1.5;
    color: #F1ECE0;
    margin-bottom: 34px;
    max-width: 800px;
    text-shadow: 0 2px 8px rgba(0, 0, 0, 0.35);
  }
  .actions {
    display: flex;
    align-items: center;
    gap: 20px;
  }
  .btn-cta {
    background: linear-gradient(135deg, ${b.secondaryColor} 0%, #D4901B 100%);
    color: #12281B;
    font-weight: 700;
    font-size: 18px;
    padding: 16px 36px;
    border-radius: 9999px;
    display: inline-flex;
    align-items: center;
    gap: 10px;
    box-shadow: 0 8px 25px rgba(227, 167, 47, 0.4);
    border: none;
  }
  .code-pill {
    background: rgba(0, 0, 0, 0.35);
    border: 1px dashed ${b.accentColor};
    color: ${b.accentColor};
    font-size: 16px;
    font-weight: 600;
    padding: 14px 24px;
    border-radius: 9999px;
    backdrop-filter: blur(8px);
  }
  /* Right Illustration Graphic */
  .visual-card {
    position: relative;
    width: 440px;
    height: 440px;
    border-radius: 28px;
    background: rgba(255, 255, 255, 0.05);
    border: 1px solid rgba(255, 255, 255, 0.15);
    backdrop-filter: blur(16px);
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: center;
    padding: 30px;
    text-align: center;
    box-shadow: 0 20px 50px rgba(0,0,0,0.3);
  }
  .visual-icon {
    font-size: 90px;
    margin-bottom: 20px;
    filter: drop-shadow(0 10px 20px rgba(0,0,0,0.35));
  }
  .visual-card h3 {
    font-family: 'Cormorant Garamond', Georgia, serif;
    font-size: 32px;
    color: ${b.accentColor};
    margin-bottom: 10px;
  }
  .visual-card p {
    font-size: 15px;
    color: #DDD4C4;
    line-height: 1.4;
  }
  .trust-row {
    margin-top: 20px;
    display: flex;
    gap: 14px;
    font-size: 13px;
    color: #F7C85C;
    font-weight: 600;
  }
</style>
</head>
<body>
  <div class="bg-glow"></div>
  <div class="bg-glow-left"></div>

  <!-- Sacred Geometry Mandala Background -->
  <svg class="mandala-svg" viewBox="0 0 200 200" fill="none" stroke="${b.accentColor}">
    <circle cx="100" cy="100" r="90" stroke-width="1" stroke-dasharray="4 4" />
    <circle cx="100" cy="100" r="70" stroke-width="1.5" />
    <circle cx="100" cy="100" r="50" stroke-width="1" />
    <circle cx="100" cy="100" r="30" stroke-width="1.5" stroke-dasharray="2 2" />
    <polygon points="100,10 178,55 178,145 100,190 22,145 22,55" stroke-width="1" />
    <polygon points="100,190 22,145 22,55 100,10 178,55 178,145" stroke-width="1" transform="rotate(30 100 100)" />
  </svg>

  <div class="container">
    <div class="content">
      <div class="tagline-badge">
        ✦ ${b.tagline}
      </div>
      <h1 class="title">${b.title}</h1>
      <p class="desc">${b.desc}</p>
      <div class="actions">
        <div class="btn-cta">${b.cta} →</div>
        <div class="code-pill">${b.code}</div>
      </div>
    </div>

    <div class="visual-card">
      <div class="visual-icon">${getIcon(b.theme)}</div>
      <h3>${getCardTitle(b.theme)}</h3>
      <p>${getCardDesc(b.theme)}</p>
      <div class="trust-row">
        <span>✓ 100% Classical</span>
        <span>✓ Qualified Vaidyas</span>
        <span>✓ Classical Standards</span>
      </div>
    </div>
  </div>
</body>
</html>`;
}

function getIcon(theme) {
    switch (theme) {
        case 'diwali': return '🪔';
        case 'navratri': return '🌿';
        case 'dhanteras': return '🏺';
        case 'winter': return '🏔️';
        case 'consult': return '🩺';
        case 'yoga': return '🧘';
        case 'panchakarma': return '🥣';
        case 'herbs': return '🌱';
        case 'skin': return '✨';
        case 'dinacharya': return '☀️';
        default: return '🍃';
    }
}

function getCardTitle(theme) {
    switch (theme) {
        case 'diwali': return 'Ojas Radiance';
        case 'navratri': return 'Sattvic Shuddhi';
        case 'dhanteras': return 'Dhanwantari Krupa';
        case 'winter': return 'Prana Suraksha';
        case 'consult': return 'Nadi Pariksha';
        case 'yoga': return 'Chakra Santulan';
        case 'panchakarma': return 'Panchakarma Vidhi';
        case 'herbs': return 'Shuddha Vanaspati';
        case 'skin': return 'Kumkumadi Saundarya';
        case 'dinacharya': return 'Ritucharya Vidhi';
        default: return 'Ayurveda Arogya';
    }
}

function getCardDesc(theme) {
    switch (theme) {
        case 'diwali': return 'Illuminating your vital energy with authentic Rasayanas and pure cow ghee formulations.';
        case 'navratri': return 'Deep cellular reset and digestive balance during seasonal transitions.';
        case 'dhanteras': return 'Classical brass wellness vessels and premium herbal gift sets for eternal health.';
        case 'winter': return 'Fortifying ojas and respiratory immunity against winter cold and dry vata.';
        case 'consult': return 'Personalized root-cause diagnosis by licensed BAMS Ayurvedic practitioners.';
        case 'yoga': return 'Harmonizing mind, breath, and body through sacred botanical infusions and asanas.';
        case 'panchakarma': return 'Five-fold systemic purification tools crafted according to the Charaka Samhita.';
        case 'herbs': return 'Single-origin wild herbs hand-harvested from pristine Himalayan valleys.';
        case 'skin': return 'Pure Kashmiri saffron and botanical elixirs for ageless complexion.';
        case 'dinacharya': return 'Time-tested daily rituals aligning your biological clock with nature.';
        default: return 'Pure Ayurvedic care.';
    }
}

async function run() {
    console.log('Generating 10 original 1920x640 banner WebP images...');
    const browser = await chromium.launch({ headless: true });

    for (const b of BANNERS) {
        const page = await browser.newPage({ viewport: { width: 1920, height: 640 } });
        const html = generateHtml(b);
        await page.setContent(html);
        await page.waitForTimeout(300);

        // Convert page screenshot to WebP buffer
        const filePath = path.join(OUTPUT_DIR, b.filename);
        
        // Take PNG screenshot first
        const pngBuffer = await page.screenshot({ type: 'png', omitBackground: false });
        
        // In browser context, render PNG into canvas and export as WebP
        const webpDataUrl = await page.evaluate(async (pngBase64) => {
            return new Promise((resolve) => {
                const img = new Image();
                img.onload = () => {
                    const canvas = document.createElement('canvas');
                    canvas.width = 1920;
                    canvas.height = 640;
                    const ctx = canvas.getContext('2d');
                    ctx.drawImage(img, 0, 0);
                    resolve(canvas.toDataURL('image/webp', 0.92));
                };
                img.src = 'data:image/png;base64,' + pngBase64;
            });
        }, pngBuffer.toString('base64'));

        const base64Data = webpDataUrl.replace(/^data:image\/webp;base64,/, '');
        fs.writeFileSync(filePath, Buffer.from(base64Data, 'base64'));
        const stat = fs.statSync(filePath);
        console.log(`✅ Generated: ${b.filename} (${(stat.size / 1024).toFixed(1)} KB, 1920x640 WebP)`);

        await page.close();
    }

    await browser.close();
    console.log('All 10 original 1920x640 WebP banners created successfully!');
}

run().catch(err => {
    console.error('Failed to generate banners:', err);
    process.exit(1);
});
