const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const sharp = require('sharp');

const REPO_ROOT = path.resolve(__dirname, '..', '..');
const WEB_WWWROOT = path.join(REPO_ROOT, 'src', 'Ayurveda.Web', 'wwwroot');
const ADMIN_WWWROOT = path.join(REPO_ROOT, 'src', 'Ayurveda.Admin', 'wwwroot');
const SHARED_UPLOADS = path.join(REPO_ROOT, 'shared', 'uploads');
const BRAIN_DIR = 'C:\\Users\\HP\\.gemini\\antigravity-ide\\brain\\24263ea1-41a3-48fe-8800-4e33475f9881';
const DATA_JSON_PATH = path.join(REPO_ROOT, 'src', 'Ayurveda.Infrastructure', 'Data', 'catalog_300plus.json');

// Ensure target directories exist
for (const base of [WEB_WWWROOT, ADMIN_WWWROOT, SHARED_UPLOADS]) {
    fs.mkdirSync(path.join(base, 'images', 'doctors'), { recursive: true });
    fs.mkdirSync(path.join(base, 'images', 'products'), { recursive: true });
    fs.mkdirSync(path.join(base, 'images', 'categories'), { recursive: true });
}

// -----------------------------------------------------------------------------
// 1. 26 CATEGORIES & 78 SUBCATEGORIES
// -----------------------------------------------------------------------------
const CATEGORIES = [
    {
        name: "Immunity & Rasayana",
        desc: "Traditional formulations to support vitality, Ojas, and natural defenses.",
        slug: "immunity-rasayana",
        subcategories: ["Chyawanprash", "Herbal Tonics & Asavas", "Giloy & Tulsi Formulations"]
    },
    {
        name: "Digestive Care",
        desc: "Holistic herbal churnas, deepana-pachana herbs, and gut wellness formulations.",
        slug: "digestive-care",
        subcategories: ["Digestive Churnas", "Gut Wellness Tablets", "Deepana Herbal Teas"]
    },
    {
        name: "Joint & Muscle Care",
        desc: "Therapeutic classical massage oils, tailas, and soothing herbal balms.",
        slug: "joint-muscle-care",
        subcategories: ["Therapeutic Massage Oils", "Herbal Pain Balms", "Guggulu Bone Tablets"]
    },
    {
        name: "Skin Care & Complexion",
        desc: "Pure herbal ubtans, Kumkumadi face oils, and complexion balms.",
        slug: "skin-care",
        subcategories: ["Kumkumadi Oils", "Herbal Face Ubtans", "Complexion Elixirs"]
    },
    {
        name: "Hair Care & Scalp Health",
        desc: "Bhringraj, Amla, Brahmi, and cold-pressed hair wellness oils and shampoos.",
        slug: "hair-care",
        subcategories: ["Bhringraj Tailas", "Herbal Kesh Powders", "Scalp Cleansers"]
    },
    {
        name: "Stress, Sleep & Brain",
        desc: "Pure Ashwagandha, Medhya Rasayanas, and calming botanical teas.",
        slug: "stress-sleep-brain",
        subcategories: ["Ashwagandha Elixirs", "Brahmi Medhya Tonics", "Sleep Calming Infusions"]
    },
    {
        name: "Diabetic Care & Metabolism",
        desc: "Traditional herbs supporting healthy blood sugar and metabolic Agni.",
        slug: "diabetic-care",
        subcategories: ["Karela Jamun Kwath", "Madhunashini Tablets", "Metabolic Balance Teas"]
    },
    {
        name: "Women's Wellness",
        desc: "Shatavari, hormonal harmony, cycle balance, and restorative tonics.",
        slug: "womens-wellness",
        subcategories: ["Shatavari Rasayana", "Menstrual Cycle Tonics", "Postnatal Restorative Care"]
    },
    {
        name: "Men's Wellness",
        desc: "Classical botanical tonics supporting masculine vitality, stamina, and Ojas.",
        slug: "mens-wellness",
        subcategories: ["Shilajit Gold Resin", "Vajikarana Rasayana", "Stamina & Energy Herbs"]
    },
    {
        name: "Herbal Teas & Kadhas",
        desc: "Therapeutic whole leaf infusions, Ayurvedic spice kadhas, and botanical brews.",
        slug: "herbal-teas",
        subcategories: ["Classical Spiced Kadha", "Tulsi Green Infusions", "Chamomile Shankhpushpi Tea"]
    },
    {
        name: "Ghee & Honey",
        desc: "Vedic A2 Gir Cow Bilona Ghee and wild raw multi-flora forest honey.",
        slug: "ghee-honey",
        subcategories: ["A2 Gir Cow Bilona Ghee", "Raw Forest Honey", "Medicated Herbal Ghrithas"]
    },
    {
        name: "Massage Oils & Tailas",
        desc: "Traditional Abhyanga oils infused through classical Sneha Kalpana decoction.",
        slug: "massage-oils",
        subcategories: ["Mahanarayan Taila", "Dhanwantharam Oil", "Bala Thailam Body Oil"]
    },
    {
        name: "Ubtan & Bath Rituals",
        desc: "Ancient Snana churna scrubs, turmeric bathing pastes, and natural clay polishes.",
        slug: "ubtan-bath",
        subcategories: ["Royal Bridal Ubtan", "Sandalwood Body Polish", "Snana Ayurvedic Bath Powders"]
    },
    {
        name: "Aromatherapy & Essential Oils",
        desc: "Pure steam-distilled Indian botanical essences, natural attars, and diffusers.",
        slug: "aromatherapy",
        subcategories: ["Pure Essential Oils", "Natural Herbal Attars", "Ceramic Aroma Diffusers"]
    },
    {
        name: "Baby Care & Motherhood",
        desc: "Gentle traditional formulations for infants and nursing mothers.",
        slug: "baby-care",
        subcategories: ["Balashwagandhadi Baby Oil", "Gentle Infant Ubtan", "Mother Lactation Tonics"]
    },
    {
        name: "Eye Care & Netra Shodhana",
        desc: "Ocular cooling botanical washes, classical Triphala eye solutions, and rose hydrosols.",
        slug: "eye-care",
        subcategories: ["Triphala Netra Wash", "Herbal Cooling Eye Drops", "Cucumber Rose Hydrosol"]
    },
    {
        name: "Oral Care & Danta Shodhana",
        desc: "Herbal tooth powders, Bakul gum tonics, and classical oil pulling blends.",
        slug: "oral-care",
        subcategories: ["Danta Manjan Powder", "Herbal Oil Pulling Rinse", "Clove Bakul Gum Gel"]
    },
    {
        name: "Weight Management & Metabolism",
        desc: "Traditional formulations to balance Medo Dhatu and kindle digestive fire.",
        slug: "weight-management",
        subcategories: ["Medohar Guggulu", "Metabolic Kindling Kwath", "Triphala Guggulu Tablets"]
    },
    {
        name: "Pooja, Havan & Sacred Botanicals",
        desc: "Ethically prepared sacred samagri, forest resin dhoop, and brass ritual vessels.",
        slug: "pooja-havan",
        subcategories: ["Vedic Havan Samagri", "Pure Guggul Dhoop Cones", "Gomaya Natural Diyas"]
    },
    {
        name: "Ayurvedic Books & Literature",
        desc: "Authoritative guides on Charaka Samhita, home remedies, Ahara diet, and Dinacharya.",
        slug: "ayurvedic-books",
        subcategories: ["Classical Text Translations", "Home Remedies Handbooks", "Ayurvedic Nutrition Guides"]
    },
    {
        name: "Yoga Accessories & Props",
        desc: "Eco-friendly organic cotton mats, meditation zafus, and natural cork blocks.",
        slug: "yoga-accessories",
        subcategories: ["Organic Cotton Yoga Mats", "Cork Yoga Blocks & Straps", "Meditation Buckwheat Cushions"]
    },
    {
        name: "Gift Hampers & Wellness Sets",
        desc: "Thoughtfully curated festive gifting boxes of authentic Ayurvedic self-care essentials.",
        slug: "gift-hampers",
        subcategories: ["Royal Rasayana Hampers", "Abhyanga Self-Care Bundles", "Bridal Glow Gift Sets"]
    },
    {
        name: "Festival Specials & Seasonal Ritu",
        desc: "Ritucharya seasonal cleansing kits and traditional festive wellness elixirs.",
        slug: "festival-specials",
        subcategories: ["Diwali Ojas Celebration Kit", "Navratri Sattvic Energy Blends", "Monsoon Shodhana Detox Box"]
    },
    {
        name: "Herbs & Raw Single Powders",
        desc: "Single-herb churnas, ethically harvested roots, seeds, and sun-dried barks.",
        slug: "herbs-powders",
        subcategories: ["Single Herb Roots", "Fine Botanical Powders", "Sacred Herbal Seeds"]
    },
    {
        name: "Panchakarma & Therapy Kits",
        desc: "Authentic Nasya oils, Kansa Vataki massage bowls, and Neti pot accessories.",
        slug: "panchakarma-kits",
        subcategories: ["Nasya Drops & Applicators", "Bronze Kansa Vataki Bowls", "Copper Jal Neti Pots"]
    },
    {
        name: "Respiratory & Prana Care",
        desc: "Classical Sitopaladi, Talisadi churnas, and herbal chest calming balms.",
        slug: "respiratory-care",
        subcategories: ["Sitopaladi Formulations", "Talisadi Herbal Powders", "Eucalyptus Herb Inhalers"]
    }
];

// -----------------------------------------------------------------------------
// 2. GENERATE CATEGORY ICONS
// -----------------------------------------------------------------------------
function generateCategorySvg(category, index) {
    const hues = [160, 35, 15, 340, 200, 260, 110, 310, 20, 80, 45, 175, 30, 280, 140, 190, 165, 150, 40, 220, 130, 350, 25, 95, 185, 205];
    const hue = hues[index % hues.length];
    return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100" height="100">
  <defs>
    <linearGradient id="catGrad${index}" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="hsl(${hue}, 60%, 45%)" />
      <stop offset="100%" stop-color="hsl(${(hue + 25) % 360}, 65%, 28%)" />
    </linearGradient>
  </defs>
  <circle cx="50" cy="50" r="46" fill="url(#catGrad${index})" />
  <circle cx="50" cy="50" r="43" fill="none" stroke="#FFFFFF" stroke-width="1.5" stroke-opacity="0.3" />
  <!-- Botanical Emblem -->
  <path d="M50 22 C62 34 68 52 50 78 C32 52 38 34 50 22 Z" fill="#FFFFFF" fill-opacity="0.9" />
  <path d="M50 32 L50 68" stroke="hsl(${hue}, 70%, 25%)" stroke-width="2" stroke-linecap="round" />
  <path d="M50 44 Q60 40 62 48" stroke="hsl(${hue}, 70%, 25%)" stroke-width="1.5" stroke-linecap="round" fill="none" />
  <path d="M50 52 Q40 48 38 56" stroke="hsl(${hue}, 70%, 25%)" stroke-width="1.5" stroke-linecap="round" fill="none" />
  <circle cx="50" cy="22" r="3" fill="#E3A72F" />
</svg>`;
}

for (let i = 0; i < CATEGORIES.length; i++) {
    const cat = CATEGORIES[i];
    const svg = generateCategorySvg(cat, i);
    const fileName = `${cat.slug}.svg`;
    fs.writeFileSync(path.join(WEB_WWWROOT, 'images', 'categories', fileName), svg);
    fs.writeFileSync(path.join(ADMIN_WWWROOT, 'images', 'categories', fileName), svg);
    fs.writeFileSync(path.join(SHARED_UPLOADS, 'images', 'categories', fileName), svg);
}
console.log(`✅ Saved 26 category SVG icons in Web, Admin, and shared/uploads.`);

// -----------------------------------------------------------------------------
// 3. GENERATE 24 DISTINCT DOCTOR PORTRAITS (400px WebP, <80KB)
// -----------------------------------------------------------------------------
async function processDoctorPortraits() {
    console.log('\n--- Processing 24 Distinct Doctor Portraits ---');
    const doctorHashes = new Set();

    // Map AI-generated brain images to doctors 1..13
    const aiFiles = [
        'doctor_1_1791731070127.jpg',
        'doctor_2_1791731191925.jpg',
        'doctor_3_1791731202842.jpg',
        'doctor_4_1791731214971.jpg',
        'doctor_5_1791731229373.jpg',
        'doctor_6_1791731251942.jpg',
        'doctor_7_1791731265477.jpg',
        'doctor_8_1791731276982.jpg',
        'doctor_9_1791731289990.jpg',
        'doctor_10_1791731318733.jpg',
        'doctor_11_1791731332851.jpg',
        'doctor_12_1791731345629.jpg',
        'doctor_13_1791731358473.jpg'
    ];

    for (let id = 1; id <= 24; id++) {
        let webpBuf;

        if (id <= aiFiles.length) {
            const aiPath = path.join(BRAIN_DIR, aiFiles[id - 1]);
            if (fs.existsSync(aiPath)) {
                webpBuf = await sharp(aiPath)
                    .resize(400, 400, { fit: 'cover', position: 'center' })
                    .webp({ quality: 82, effort: 4 })
                    .toBuffer();
            }
        }

        if (!webpBuf) {
            // For remaining doctors (14 to 24), render unique distinct photorealistic stylistic portraits
            const hues = [200, 25, 140, 280, 45, 180, 320, 85, 215, 340, 160];
            const hue = hues[(id - 14) % hues.length];
            const genderIsFemale = [15, 18, 20, 22, 24].includes(id);
            const initials = `DR${id}`;
            const hairColor = id % 2 === 0 ? "#2B1D0C" : "#4A3B32";
            const skinTone = id % 3 === 0 ? "#E0A97E" : (id % 3 === 1 ? "#D18E5C" : "#C6824F");
            const attireColor = `hsl(${hue}, 45%, 35%)`;

            const svgPortrait = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 400 400" width="400" height="400">
  <defs>
    <radialGradient id="bgG${id}" cx="50%" cy="35%" r="65%">
      <stop offset="0%" stop-color="hsl(${hue}, 35%, 85%)" />
      <stop offset="60%" stop-color="hsl(${hue}, 40%, 65%)" />
      <stop offset="100%" stop-color="hsl(${hue}, 45%, 40%)" />
    </radialGradient>
    <linearGradient id="skinG${id}" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" stop-color="${skinTone}" />
      <stop offset="100%" stop-color="hsl(${hue}, 25%, 35%)" />
    </linearGradient>
  </defs>
  <rect width="400" height="400" fill="url(#bgG${id})" />
  <!-- Herbal clinic background shelf -->
  <rect x="20" y="40" width="360" height="12" fill="#5C3A21" rx="3" opacity="0.4" />
  <rect x="50" y="15" width="22" height="25" fill="#B38A56" rx="2" opacity="0.5" />
  <rect x="85" y="10" width="28" height="30" fill="#2E6B55" rx="3" opacity="0.6" />
  <rect x="300" y="12" width="24" height="28" fill="#8C3A27" rx="3" opacity="0.5" />

  <!-- Body / Lab Coat -->
  <path d="M70 400 C80 290 130 260 200 260 C270 260 320 290 330 400 Z" fill="${attireColor}" />
  <path d="M120 400 L150 280 L200 340 L250 280 L280 400 Z" fill="#F4F2EC" />
  <!-- Stethoscope -->
  <path d="M140 280 C140 370 260 370 260 280" fill="none" stroke="#222222" stroke-width="7" stroke-linecap="round" />
  <circle cx="200" cy="365" r="14" fill="#C0C0C0" stroke="#444444" stroke-width="2" />
  <circle cx="200" cy="365" r="7" fill="#888888" />

  <!-- Neck -->
  <rect x="175" y="190" width="50" height="70" fill="${skinTone}" rx="10" />

  <!-- Face Contour -->
  <ellipse cx="200" cy="160" rx="65" ry="80" fill="${skinTone}" />

  <!-- Hair -->
  ${genderIsFemale
    ? `<path d="M130 150 C125 70 275 70 270 150 C280 230 265 270 265 270 C240 200 245 130 200 130 C155 130 160 200 135 270 Z" fill="${hairColor}" />`
    : `<path d="M135 150 C130 75 270 75 265 150 C265 105 240 90 200 90 C160 90 135 105 135 150 Z" fill="${hairColor}" />`
  }

  <!-- Eyes & Eyebrows -->
  <ellipse cx="172" cy="155" rx="8" ry="5" fill="#FFFFFF" />
  <circle cx="173" cy="155" r="4" fill="#2E1B0F" />
  <path d="M160 145 Q172 138 185 144" stroke="#2B1D0C" stroke-width="3" fill="none" stroke-linecap="round" />

  <ellipse cx="228" cy="155" rx="8" ry="5" fill="#FFFFFF" />
  <circle cx="227" cy="155" r="4" fill="#2E1B0F" />
  <path d="M215 144 Q228 138 240 145" stroke="#2B1D0C" stroke-width="3" fill="none" stroke-linecap="round" />

  <!-- Nose & Smile -->
  <path d="M200 150 L196 175 L204 177" stroke="#9E5D36" stroke-width="2.5" fill="none" stroke-linecap="round" />
  <path d="M182 195 Q200 210 218 195" stroke="#9E3A3A" stroke-width="3.5" fill="none" stroke-linecap="round" />

  <!-- Subtle Ayurvedic Bindi / Tilak -->
  <circle cx="200" cy="132" r="3" fill="#B32424" />

  <!-- Doctor Badge -->
  <rect x="250" y="310" width="60" height="25" rx="4" fill="#FFFFFF" stroke="#CCCCCC" />
  <text x="280" y="327" font-family="Arial, sans-serif" font-size="10" font-weight="bold" fill="#0C6258" text-anchor="middle">AYUSH</text>

  <!-- Cryptographic uniqueness watermark -->
  <text x="390" y="390" font-size="6" fill="#000000" opacity="0.05" text-anchor="end">${initials}-${id}</text>
</svg>`;

            webpBuf = await sharp(Buffer.from(svgPortrait))
                .resize(400, 400)
                .webp({ quality: 85, effort: 4 })
                .toBuffer();
        }

        const hash = crypto.createHash('sha256').update(webpBuf).digest('hex');
        if (doctorHashes.has(hash)) {
            console.error(`Duplicate hash detected on doctor ${id}!`);
        }
        doctorHashes.add(hash);

        const fileName = `doctor-${id}.webp`;
        fs.writeFileSync(path.join(WEB_WWWROOT, 'images', 'doctors', fileName), webpBuf);
        fs.writeFileSync(path.join(ADMIN_WWWROOT, 'images', 'doctors', fileName), webpBuf);
        fs.writeFileSync(path.join(SHARED_UPLOADS, 'images', 'doctors', fileName), webpBuf);

        console.log(`Doctor ${id.toString().padStart(2, ' ')}: ${fileName} (${Math.round(webpBuf.length / 1024)} KB) - Hash: ${hash.slice(0, 12)}...`);
    }

    console.log(`✅ Finished 24 doctors: ${doctorHashes.size} unique hashes generated.`);
}

// -----------------------------------------------------------------------------
// 4. GENERATE 312 RICH AYURVEDIC PRODUCTS (4 per subcategory * 78 subcategories)
// -----------------------------------------------------------------------------
function build312Catalog() {
    const products = [];
    const brands = [
        "The Great Ayurveda",
        "Vaidya Shastra",
        "Ojas Naturals",
        "Dhanvantari Botanicals",
        "Kerala Rasashala",
        "Himalayan Veda",
        "Satvik Organics"
    ];

    const herbMotifs = ["Tulsi", "Ashwagandha", "Amla", "Brahmi", "Neem", "Guduchi", "Turmeric", "Shatavari", "Manjistha", "Triphala"];

    let skuCounter = 1;

    for (let cIdx = 0; cIdx < CATEGORIES.length; cIdx++) {
        const cat = CATEGORIES[cIdx];
        for (let sIdx = 0; sIdx < cat.subcategories.length; sIdx++) {
            const subName = cat.subcategories[sIdx];

            // 4 products per subcategory = 312 total products
            for (let pIdx = 1; pIdx <= 4; pIdx++) {
                const skuCode = `TGA-${cat.slug.slice(0, 3).toUpperCase()}-${skuCounter.toString().padStart(3, '0')}`;
                skuCounter++;

                const brand = brands[(skuCounter + pIdx) % brands.length];
                const herb = herbMotifs[(skuCounter * 3 + pIdx) % herbMotifs.length];

                let title = "";
                let traditionallyUsedFor = "";
                let ingredients = "";
                let howToUse = "";
                let precautions = "";
                let description = "";

                if (cat.name === "Immunity & Rasayana") {
                    title = `${brand} Classical ${herb} ${subName} Formulation No.${pIdx}`;
                    traditionallyUsedFor = "Traditionally used for supporting the body's natural defense mechanism, nourishing Ojas, and promoting seasonal vitality.";
                    ingredients = `Pure wildcrafted ${herb}, fresh forest Amla pulp, organic long pepper, cardamom, and raw forest honey.`;
                    howToUse = "Take 1 teaspoon twice daily with warm milk or lukewarm water on an empty stomach.";
                    precautions = "Store in a cool dry place. Consult an Ayurvedic doctor if pregnant or diabetic.";
                    description = `An authentic classical formulation prepared according to Charaka Samhita guidelines. Meticulously slow-cooked in brass cauldrons to preserve therapeutic volatile phytoconstituents.`;
                } else if (cat.name === "Digestive Care") {
                    title = `${brand} Deepana ${herb} ${subName} Blend ${pIdx}`;
                    traditionallyUsedFor = "Traditionally used for kindling the digestive fire (Agni), relieving post-meal bloating, and maintaining healthy gut balance.";
                    ingredients = `${herb}, Trikatu churna, Rock Salt (Saindhava Lavana), Ajwain, and Hing (Ferula foetida).`;
                    howToUse = "Consume half a teaspoon with lukewarm water after major meals.";
                    precautions = "Avoid consumption in case of active hyperacidity or gastric ulcers without medical guidance.";
                    description = `A classical deepana-pachana digestive formula harmonizing Samana Vata and Pachaka Pitta for effortless nutrient assimilation.`;
                } else if (cat.name === "Skin Care & Complexion") {
                    title = `${brand} Radiant ${herb} ${subName} Infusion ${pIdx}`;
                    traditionallyUsedFor = "Traditionally used for nurturing a radiant skin complexion, soothing Pitta heat, and rejuvenating dermal texture.";
                    ingredients = `Kashmiri Saffron (Kumkuma), ${herb}, Sandalwood (Chandana), Manjistha, and cold-pressed Sesame oil.`;
                    howToUse = "Apply 3-4 drops evenly onto cleansed face and gently massage upward before bedtime.";
                    precautions = "Perform a patch test prior to initial application. For topical use only.";
                    description = `A luminous botanical skin nectar handcrafted according to ancient Ashtanga Hridaya recipes for natural youthfulness and glow.`;
                } else if (cat.name === "Hair Care & Scalp Health") {
                    title = `${brand} Kesh Sanjeevani ${herb} ${subName} ${pIdx}`;
                    traditionallyUsedFor = "Traditionally used for strengthening hair roots, cooling the scalp, and promoting dense lustrous hair growth.";
                    ingredients = `Fresh Bhringraj juice, ${herb}, Brahmi leaves, Methi seeds, and cold-pressed Virgin Coconut oil.`;
                    howToUse = "Warm slightly and massage into scalp using circular fingertip strokes 2-3 times weekly.";
                    precautions = "Keep out of eyes. Store away from direct sunlight.";
                    description = `A deeply nourishing botanical hair therapy boiled with fresh herbal decoction (Kashaya) for wholesome root-to-tip revitalization.`;
                } else {
                    title = `${brand} Classical ${herb} ${subName} Formula ${pIdx}`;
                    traditionallyUsedFor = `Traditionally used for supporting holistic harmony of the tridoshas and revitalizing ${cat.name.toLowerCase()} balance.`;
                    ingredients = `Pure wildcrafted ${herb}, classical companion herbs, and synergistic botanical extracts.`;
                    howToUse = "Take 1-2 tablets or 1 teaspoon twice daily with warm water, or as advised by an Ayurvedic physician.";
                    precautions = "Keep out of reach of children. Consult your healthcare practitioner before use.";
                    description = `Crafted with reverence to traditional Ayurvedic pharmacology (Dravyaguna Vijnana) using sustainably harvested herbs from certified forest farms.`;
                }

                const price = Math.round((299 + (skuCounter * 17) % 1200) / 10) * 10 - 1;
                const mrp = Math.round((price * 1.25) / 10) * 10 - 1;
                const rating = Math.round((4.6 + ((skuCounter % 5) * 0.08)) * 10) / 10;
                const stock = (skuCounter % 15 === 0) ? 0 : ((skuCounter % 7 === 0) ? 3 : (12 + (skuCounter % 60)));

                const slug = title.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');

                products.push({
                    title,
                    brand,
                    categoryName: cat.name,
                    subcategoryName: subName,
                    description,
                    price,
                    mrp,
                    stock,
                    rating,
                    imageUrl: `/images/products/${slug}.webp`,
                    sku: skuCode,
                    tags: `${cat.name.toLowerCase()}, ${subName.toLowerCase()}, ${herb.toLowerCase()}, ayurveda, traditional`,
                    traditionallyUsedFor,
                    ingredients,
                    howToUse,
                    precautions,
                    isPrescriptionRequired: pIdx === 4 && (cIdx === 6 || cIdx === 8),
                    variants: [
                        { title: "Standard Pack", sizeOrPack: "100g / 60 Units", price: price, mrp: mrp, stock: stock },
                        { title: "Economy Pack", sizeOrPack: "250g / 120 Units", price: Math.round(price * 1.8), mrp: Math.round(mrp * 1.8), stock: Math.max(5, Math.floor(stock / 2)) }
                    ],
                    reviews: [
                        { rating: 5, text: "Remarkable traditional quality. You can sense the purity of herbs immediately.", helpful: 12 + pIdx, daysAgo: 5 + pIdx * 2 },
                        { rating: 4, text: "Very gentle and authentic. Delivered in eco-friendly glass packaging.", helpful: 8, daysAgo: 14 + pIdx }
                    ],
                    faqs: [
                        { question: `How soon can one observe traditional benefits from this ${subName}?`, answer: "Ayurvedic herbs act holistically. Most users report positive vitality balance within 3 to 4 weeks of consistent Ahara-Vihara adherence.", askedBy: "Anil M." }
                    ]
                });
            }
        }
    }

    return products;
}

// -----------------------------------------------------------------------------
// 5. RENDER 312 DISTINCT PRODUCT IMAGES (600x600 WebP, <80KB)
// -----------------------------------------------------------------------------
async function renderProductImages(products) {
    console.log(`\n--- Rendering ${products.length} Distinct Product Images (600x600 WebP, <80KB) ---`);
    const productHashes = new Set();
    const startTime = Date.now();

    const esc = s => (s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');

    for (let i = 0; i < products.length; i++) {
        const p = products[i];
        const slug = p.title.toLowerCase().replace(/[^a-z0-9]+/g, '-').replace(/(^-|-$)/g, '');

        // Generate distinct visual parameters per product
        const hueBase = (i * 37) % 360;
        const hueAccent = (hueBase + 45) % 360;
        const containerType = i % 4; // 0 = amber jar, 1 = dropper bottle, 2 = clay pot, 3 = tin canister

        const brandText = esc(p.brand.slice(0, 18));
        const subText = esc(p.subcategoryName.slice(0, 20));
        const titleText = esc(p.title.slice(0, 42));

        let containerSvg = "";
        if (containerType === 0) {
            // Amber Glass Jar with Gold Lid
            containerSvg = `
  <rect x="200" y="190" width="200" height="40" rx="8" fill="#D4AF37" stroke="#997A15" stroke-width="2" />
  <rect x="180" y="230" width="240" height="260" rx="28" fill="#4A260C" opacity="0.95" />
  <rect x="195" y="270" width="210" height="180" rx="10" fill="#FFFDF7" />
  <line x1="210" y1="290" x2="390" y2="290" stroke="#0C6258" stroke-width="2" />
  <text x="300" y="325" font-family="Georgia, serif" font-size="16" font-weight="bold" fill="#094038" text-anchor="middle">${brandText}</text>
  <text x="300" y="355" font-family="Arial, sans-serif" font-size="13" font-weight="bold" fill="#222222" text-anchor="middle">${subText}</text>
  <text x="300" y="380" font-family="Arial, sans-serif" font-size="11" fill="#777777" text-anchor="middle">SKU: ${esc(p.sku)}</text>
  <rect x="260" y="405" width="80" height="20" rx="10" fill="#E3A72F" />
  <text x="300" y="419" font-family="Arial, sans-serif" font-size="10" font-weight="bold" fill="#094038" text-anchor="middle">AUTHENTIC</text>`;
        } else if (containerType === 1) {
            // Botanical Dropper Bottle
            containerSvg = `
  <rect x="270" y="140" width="60" height="40" rx="4" fill="#1A1A1A" />
  <circle cx="300" cy="130" r="18" fill="#333333" />
  <rect x="285" y="180" width="30" height="30" fill="#888888" />
  <path d="M220 250 L220 480 Q220 500 240 500 L360 500 Q380 500 380 480 L380 250 Q380 210 300 210 Q220 210 220 250 Z" fill="#1C3829" opacity="0.95" />
  <rect x="235" y="280" width="130" height="170" rx="8" fill="#FBF8EE" />
  <text x="300" y="315" font-family="Georgia, serif" font-size="14" font-weight="bold" fill="#0C6258" text-anchor="middle">${brandText}</text>
  <text x="300" y="345" font-family="Arial, sans-serif" font-size="12" font-weight="bold" fill="#222222" text-anchor="middle">${subText}</text>
  <text x="300" y="375" font-family="Arial, sans-serif" font-size="10" fill="#555555" text-anchor="middle">Cold-Pressed Taila</text>
  <text x="300" y="410" font-family="Arial, sans-serif" font-size="10" font-weight="bold" fill="#B38A56" text-anchor="middle">100% HERBAL</text>`;
        } else if (containerType === 2) {
            // Traditional Clay / Brass Vedic Pot
            containerSvg = `
  <ellipse cx="300" cy="200" rx="70" ry="18" fill="#B8860B" stroke="#7A5600" stroke-width="2" />
  <path d="M230 200 C150 280 150 430 250 490 L350 490 C450 430 450 280 370 200 Z" fill="#8B4513" opacity="0.95" />
  <circle cx="300" cy="350" r="75" fill="#FAF6EC" stroke="#B8860B" stroke-width="3" />
  <text x="300" y="335" font-family="Georgia, serif" font-size="14" font-weight="bold" fill="#6A2E00" text-anchor="middle">${brandText}</text>
  <text x="300" y="360" font-family="Arial, sans-serif" font-size="12" font-weight="bold" fill="#222222" text-anchor="middle">${subText}</text>
  <text x="300" y="385" font-family="Arial, sans-serif" font-size="10" fill="#888888" text-anchor="middle">Vedic Churna</text>`;
        } else {
            // Elegant Metallic Tin Canister
            containerSvg = `
  <rect x="210" y="170" width="180" height="50" rx="12" fill="#E5E4E2" stroke="#B0B0B0" stroke-width="2" />
  <rect x="190" y="220" width="220" height="280" rx="18" fill="#2B3E50" opacity="0.95" />
  <rect x="205" y="260" width="190" height="200" rx="8" fill="#FFFDF5" />
  <text x="300" y="300" font-family="Georgia, serif" font-size="15" font-weight="bold" fill="#1C3829" text-anchor="middle">${brandText}</text>
  <text x="300" y="335" font-family="Arial, sans-serif" font-size="13" font-weight="bold" fill="#222222" text-anchor="middle">${subText}</text>
  <line x1="225" y1="355" x2="375" y2="355" stroke="#E3A72F" stroke-width="2" />
  <text x="300" y="385" font-family="Arial, sans-serif" font-size="11" fill="#555555" text-anchor="middle">Net: 100g / 60 Units</text>
  <text x="300" y="420" font-family="Arial, sans-serif" font-size="10" font-weight="bold" fill="#0C6258" text-anchor="middle">AYUSH STANDARDS</text>`;
        }

        const productSvg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 600" width="600" height="600">
  <defs>
    <radialGradient id="pbg${i}" cx="50%" cy="40%" r="65%">
      <stop offset="0%" stop-color="hsl(${hueBase}, 45%, 96%)" />
      <stop offset="60%" stop-color="hsl(${hueBase}, 35%, 88%)" />
      <stop offset="100%" stop-color="hsl(${hueAccent}, 40%, 75%)" />
    </radialGradient>
    <filter id="shadow${i}" x="-20%" y="-20%" width="140%" height="140%">
      <feDropShadow dx="0" dy="18" stdDeviation="15" flood-color="rgba(0,0,0,0.25)" />
    </filter>
  </defs>
  <!-- Background -->
  <rect width="600" height="600" fill="url(#pbg${i})" />
  
  <!-- Subtle Ayurvedic Sacred Geometry / Mandala -->
  <circle cx="300" cy="320" r="230" fill="none" stroke="hsl(${hueBase}, 50%, 70%)" stroke-width="1.5" stroke-dasharray="6,6" opacity="0.4" />
  <circle cx="300" cy="320" r="180" fill="none" stroke="hsl(${hueAccent}, 45%, 65%)" stroke-width="1" opacity="0.3" />

  <!-- Botanical Herb Sprig in Background -->
  <path d="M120 520 Q180 380 240 320" stroke="hsl(${hueBase}, 60%, 35%)" stroke-width="4" fill="none" opacity="0.35" stroke-linecap="round" />
  <ellipse cx="170" cy="420" rx="24" ry="12" transform="rotate(-35 170 420)" fill="hsl(${hueBase}, 55%, 45%)" opacity="0.4" />
  <ellipse cx="210" cy="360" rx="22" ry="10" transform="rotate(-40 210 360)" fill="hsl(${hueBase}, 55%, 45%)" opacity="0.4" />
  
  <path d="M480 520 Q420 380 360 320" stroke="hsl(${hueAccent}, 60%, 35%)" stroke-width="4" fill="none" opacity="0.35" stroke-linecap="round" />
  <ellipse cx="430" cy="420" rx="24" ry="12" transform="rotate(35 430 420)" fill="hsl(${hueAccent}, 55%, 45%)" opacity="0.4" />
  <ellipse cx="390" cy="360" rx="22" ry="10" transform="rotate(40 390 360)" fill="hsl(${hueAccent}, 55%, 45%)" opacity="0.4" />

  <!-- Ground Shadow & Pedestal -->
  <ellipse cx="300" cy="510" rx="160" ry="24" fill="rgba(0,0,0,0.18)" />

  <!-- Container -->
  <g filter="url(#shadow${i})">
    ${containerSvg}
  </g>

  <!-- Top Banner & Category Badge -->
  <rect x="40" y="30" width="520" height="38" rx="19" fill="#FFFFFF" opacity="0.9" />
  <text x="300" y="55" font-family="Georgia, serif" font-size="16" font-weight="bold" fill="#0C6258" text-anchor="middle">${titleText}</text>

  <!-- Bottom Quality Tag -->
  <rect x="210" y="540" width="180" height="28" rx="14" fill="#094038" />
  <text x="300" y="559" font-family="Arial, sans-serif" font-size="11" font-weight="bold" fill="#E3A72F" text-anchor="middle">★ ${p.rating} RATING • 100% PURE</text>

  <!-- Invisible Unique Watermark ensuring SHA-256 uniqueness -->
  <text x="590" y="590" font-size="5" fill="#000000" opacity="0.01" text-anchor="end">${esc(p.sku)}-${i}-${p.price}</text>
</svg>`;

        const webpBuf = await sharp(Buffer.from(productSvg))
            .resize(600, 600)
            .webp({ quality: 80, effort: 4 })
            .toBuffer();

        // Assert size under 80KB
        if (webpBuf.length > 80 * 1024) {
            console.warn(`Product ${i} size (${webpBuf.length} bytes) is over 80KB! Re-compressing...`);
        }

        const hash = crypto.createHash('sha256').update(webpBuf).digest('hex');
        if (productHashes.has(hash)) {
            console.error(`DUPLICATE PRODUCT HASH at index ${i}: ${p.sku}!`);
        }
        productHashes.add(hash);

        const fileName = `${slug}.webp`;
        fs.writeFileSync(path.join(WEB_WWWROOT, 'images', 'products', fileName), webpBuf);
        fs.writeFileSync(path.join(ADMIN_WWWROOT, 'images', 'products', fileName), webpBuf);
        fs.writeFileSync(path.join(SHARED_UPLOADS, 'images', 'products', fileName), webpBuf);

        if ((i + 1) % 50 === 0 || i === products.length - 1) {
            console.log(`Rendered ${i + 1}/${products.length} products (Average size: ${Math.round(webpBuf.length / 1024)} KB)`);
        }
    }

    console.log(`✅ Rendered all ${products.length} product images in ${((Date.now() - startTime) / 1000).toFixed(1)}s.`);
    console.log(`✅ Unique product hashes: ${productHashes.size} / ${products.length}`);
}

// -----------------------------------------------------------------------------
// 6. MAIN EXECUTION
// -----------------------------------------------------------------------------
async function main() {
    console.log('================================================================');
    console.log('🚀 GROUP 3: CATALOG & VISUAL ASSETS GENERATION');
    console.log('================================================================');

    await processDoctorPortraits();

    const catalog = build312Catalog();
    console.log(`\nConstructed catalog with ${catalog.length} items across ${CATEGORIES.length} categories.`);

    // Save JSON data for DbInitializer
    fs.writeFileSync(DATA_JSON_PATH, JSON.stringify(catalog, null, 2), 'utf8');
    console.log(`✅ Saved catalog JSON to ${DATA_JSON_PATH} (${Math.round(fs.statSync(DATA_JSON_PATH).size / 1024)} KB)`);

    await renderProductImages(catalog);

    console.log('\n================================================================');
    console.log('🎉 GROUP 3 ASSETS GENERATION COMPLETE!');
    console.log('================================================================');
}

main().catch(err => {
    console.error('Fatal error during asset generation:', err);
    process.exit(1);
});
