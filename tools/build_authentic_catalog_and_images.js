const fs = require('fs');
const path = require('path');
const sharp = require('../tests/e2e/node_modules/sharp');

const REPO_ROOT = path.resolve(__dirname, '..');
const USER_REQ_PATH = 'C:\\Users\\HP\\.gemini\\antigravity\\brain\\3bd5ca4c-d147-4368-884c-2eccc6d77364\\scratch\\user_request_1102.txt';
const DB_CAT_PATH = path.join(REPO_ROOT, 'scratch/db_categories.json');
const OUTPUT_IMG_DIR = path.join(REPO_ROOT, 'src/Ayurveda.Web/wwwroot/images/products');
const CATALOG_OUTPUT_PATH = path.join(REPO_ROOT, 'src/Ayurveda.Infrastructure/Data/catalog_300plus.json');

if (!fs.existsSync(OUTPUT_IMG_DIR)) {
    fs.mkdirSync(OUTPUT_IMG_DIR, { recursive: true });
}

// -----------------------------------------------------------------------------
// 1. BRAND DEFINITIONS & DESIGN PALETTES
// -----------------------------------------------------------------------------
const BRAND_CONFIGS = {
    "Himalaya Wellness": {
        code: "HIM",
        slug: "himalaya",
        primary: "#1B6B44",
        accent: "#F28C28",
        labelBg: "#FFFFFF",
        sealText: "HERBAL SCIENCE • SINCE 1930",
        headerBg: "#1B6B44",
        headerText: "#FFFFFF",
        bottleColor: "#2A5B3E"
    },
    "Dabur India": {
        code: "DAB",
        slug: "dabur",
        primary: "#A81C24",
        accent: "#D4AF37",
        labelBg: "#FFFDF9",
        sealText: "TRUSTED AYURVEDA • 140 YEARS",
        headerBg: "#A81C24",
        headerText: "#FFD700",
        bottleColor: "#4A1215"
    },
    "Patanjali Ayurved": {
        code: "PAT",
        slug: "patanjali",
        primary: "#E65100",
        accent: "#2E7D32",
        labelBg: "#FFFFF8",
        sealText: "DIVYA PHARMACY • SHUDDHA",
        headerBg: "#E65100",
        headerText: "#FFFFFF",
        bottleColor: "#3D2B1F"
    },
    "Baidyanath": {
        code: "BAI",
        slug: "baidyanath",
        primary: "#800020",
        accent: "#FFD700",
        labelBg: "#FFFBF0",
        sealText: "SHREE BAIDYANATH • 100+ YEARS",
        headerBg: "#800020",
        headerText: "#FFD700",
        bottleColor: "#3B141C"
    },
    "Zandu Care": {
        code: "ZAN",
        slug: "zandu",
        primary: "#00796B",
        accent: "#FFB300",
        labelBg: "#F7FAF8",
        sealText: "ZANDU CARE • 110 YEARS TRUST",
        headerBg: "#00796B",
        headerText: "#FFFFFF",
        bottleColor: "#1A433D"
    },
    "Kottakkal Arya Vaidya Sala": {
        code: "KOT",
        slug: "kottakkal",
        primary: "#1B4D3E",
        accent: "#C5A059",
        labelBg: "#FAF6ED",
        sealText: "ARYA VAIDYA SALA KOTTAKKAL",
        headerBg: "#1B4D3E",
        headerText: "#F0E2C8",
        bottleColor: "#2C3930"
    },
    "Charak Pharma": {
        code: "CHA",
        slug: "charak",
        primary: "#00695C",
        accent: "#0277BD",
        labelBg: "#F4FBF9",
        sealText: "CHARAK PHARMA • SCIENCE OF AYURVEDA",
        headerBg: "#00695C",
        headerText: "#FFFFFF",
        bottleColor: "#1C3E38"
    },
    "AVP — The Arya Vaidya Pharmacy": {
        code: "AVP",
        slug: "avp",
        primary: "#742A2A",
        accent: "#B7791F",
        labelBg: "#FDF8F0",
        sealText: "THE ARYA VAIDYA PHARMACY",
        headerBg: "#742A2A",
        headerText: "#F9DEAA",
        bottleColor: "#3F2121"
    },
    "Maharishi Ayurveda": {
        code: "MAH",
        slug: "maharishi",
        primary: "#C8963E",
        accent: "#355E3B",
        labelBg: "#FCF9F2",
        sealText: "MAHARISHI AYURVEDA • VEDIC PURITY",
        headerBg: "#8C6521",
        headerText: "#FFF1D0",
        bottleColor: "#362A19"
    },
    "Sri Sri Tattva": {
        code: "SRI",
        slug: "sri-sri-tattva",
        primary: "#E65100",
        accent: "#2E7D32",
        labelBg: "#FFFFFF",
        sealText: "SRI SRI TATTVA • ART OF LIVING",
        headerBg: "#E65100",
        headerText: "#FFFFFF",
        bottleColor: "#2C3B2D"
    }
};

// -----------------------------------------------------------------------------
// 2. LOAD DB CATEGORIES
// -----------------------------------------------------------------------------
const rawCatStr = fs.readFileSync(DB_CAT_PATH, 'utf8').replace(/^\uFEFF/, '');
const dbCategories = JSON.parse(rawCatStr);

// Build lookup: category name -> subcategories array
const catMap = {};
dbCategories.forEach(row => {
    if (!catMap[row.CatName]) {
        catMap[row.CatName] = {
            id: row.CatId,
            name: row.CatName,
            subs: []
        };
    }
    if (row.SubId && row.SubName) {
        catMap[row.CatName].subs.push({
            id: row.SubId,
            name: row.SubName
        });
    }
});

// Helper to find category and subcategory for a product title
function classifyProduct(title) {
    const t = title.toLowerCase();

    // 1. Oral Care
    if (t.includes("toothpaste") || t.includes("toothpowder") || t.includes("red paste") || t.includes("meswak") || t.includes("babool") || t.includes("dant kanti") || t.includes("sudanta") || t.includes("gum tone") || t.includes("danta manjan") || t.includes("oil pulling")) {
        const cat = catMap["Oral Care & Danta Shodhana"];
        const sub = cat.subs.find(s => s.name.includes("Manjan") || s.name.includes("Gum")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Natural Oral Hygiene & Gum Strengthening" };
    }

    // 2. Hair Care
    if (t.includes("hair oil") || t.includes("shampoo") || t.includes("kesh kanti") || t.includes("kesh king") || t.includes("bhringraj") || t.includes("amla hair") || t.includes("vatika") || t.includes("chemparathyadi") || t.includes("bringamalakadi") || t.includes("scalp")) {
        const cat = catMap["Hair Care & Scalp Health"];
        const sub = cat.subs.find(s => s.name.includes("Bhringraj") || s.name.includes("Kesh") || s.name.includes("Cleansers")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Root Nourishment, Anti-Dandruff & Scalp Vitality" };
    }

    // 3. Respiratory Care
    if (t.includes("koflet") || t.includes("honitus") || t.includes("coronil") || t.includes("swasari") || t.includes("bronchoram") || t.includes("sitopaladi") || t.includes("trikatu") || t.includes("kasamrit") || t.includes("trishun") || t.includes("kofol") || t.includes("haleezy") || t.includes("kasni") || t.includes("asthomap") || t.includes("kasahari") || t.includes("kabasura") || t.includes("vasaka") || t.includes("inhaler") || t.includes("cough")) {
        const cat = catMap["Respiratory & Prana Care"];
        const sub = cat.subs.find(s => s.name.includes("Sitopaladi") || s.name.includes("Talisadi") || s.name.includes("Inhalers")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Respiratory Soothing, Clear Prana & Throat Comfort" };
    }

    // 4. Joint & Muscle Care
    if (t.includes("rumalaya") || t.includes("rheumartho") || t.includes("rhuma") || t.includes("orthogrit") || t.includes("rhumayog") || t.includes("fast relief") || t.includes("pirant") || t.includes("ostolief") || t.includes("pain relief") || t.includes("kaisora guggulu") || t.includes("yogaraja guggulu") || t.includes("balm") || t.includes("peedanil")) {
        const cat = catMap["Joint & Muscle Care"];
        const sub = cat.subs.find(s => s.name.includes("Balm") || s.name.includes("Bone") || s.name.includes("Massage")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Soothes Muscular Soreness & Promotes Joint Flexibility" };
    }

    // 5. Women's Wellness
    if (t.includes("evecare") || t.includes("lukol") || t.includes("shatavari") || t.includes("ashokarishta") || t.includes("dashmularishta") || t.includes("dashmoolarishta") || t.includes("m2-tone") || t.includes("evanova") || t.includes("femiplex") || t.includes("ovoutoline") || t.includes("restone") || t.includes("sukumara") || t.includes("phala sarpis")) {
        const cat = catMap["Women's Wellness"];
        const sub = cat.subs.find(s => s.name.includes("Cycle") || s.name.includes("Shatavari") || s.name.includes("Postnatal")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Hormonal Equilibrium, Female Vitality & Rejuvenation" };
    }

    // 6. Men's Wellness
    if (t.includes("confido") || t.includes("speman") || t.includes("kapikachhu") || t.includes("shilajit") || t.includes("vita-ex") || t.includes("musli pak") || t.includes("youvanamrit") || t.includes("vigorex") || t.includes("spurt") || t.includes("addizoil") || t.includes("sidpaurush") || t.includes("vajikarana") || t.includes("gokshura")) {
        const cat = catMap["Men's Wellness"];
        const sub = cat.subs.find(s => s.name.includes("Shilajit") || s.name.includes("Vajikarana") || s.name.includes("Stamina")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Stamina, Vital Essence, Vigour & Rasayana Strength" };
    }

    // 7. Stress, Sleep & Brain
    if (t.includes("mentat") || t.includes("brahmi") || t.includes("tagara") || t.includes("stresscom") || t.includes("shankhpushpi") || t.includes("memorygrit") || t.includes("neurogrit") || t.includes("brento") || t.includes("stresam") || t.includes("cognium") || t.includes("sumenta") || t.includes("blissful sleep") || t.includes("worry free") || t.includes("saraswatarishta")) {
        const cat = catMap["Stress, Sleep & Brain"];
        const sub = cat.subs.find(s => s.name.includes("Brahmi") || s.name.includes("Ashwagandha") || s.name.includes("Sleep")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Nervine Calming, Deep Sleep Support & Mental Focus" };
    }

    // 8. Diabetic Care & Metabolism
    if (t.includes("diabecon") || t.includes("karela") || t.includes("lipidom") || t.includes("madhunashini") || t.includes("glucomap") || t.includes("lean vedda") || t.includes("diabrishta") || t.includes("garcinia") || t.includes("moringa") || t.includes("renogrit") || t.includes("thyroidgrit") || t.includes("detox") || t.includes("mehantaka")) {
        const cat = catMap["Diabetic Care & Metabolism"];
        const sub = cat.subs.find(s => s.name.includes("Madhunashini") || s.name.includes("Karela") || s.name.includes("Metabolic")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Glycemic Equilibrium & Healthy Metabolic Agni" };
    }

    // 9. Skin Care & Complexion
    if (t.includes("face wash") || t.includes("face pack") || t.includes("scrub") || t.includes("body lotion") || t.includes("skin cream") || t.includes("gulabari") || t.includes("rose water") || t.includes("aloe vera gel") || t.includes("saundarya") || t.includes("multani mitti") || t.includes("pigmento") || t.includes("takzema") || t.includes("moha") || t.includes("youthful skin") || t.includes("kumkumadi") || t.includes("cucumber") || t.includes("sunscreen") || t.includes("eladi") || t.includes("nalpamaradi") || t.includes("manjishtadi") || t.includes("raktda") || t.includes("raktashodhani") || t.includes("lalima")) {
        const cat = catMap["Skin Care & Complexion"];
        const sub = cat.subs.find(s => s.name.includes("Kumkumadi") || s.name.includes("Face") || s.name.includes("Elixirs")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Skin Radiance, Blood Purification & Natural Glow" };
    }

    // 10. Baby Care
    if (t.includes("baby") || t.includes("janma ghunti") || t.includes("gripe water") || t.includes("lal tail") || t.includes("balashwagandhadi baby")) {
        const cat = catMap["Baby Care & Motherhood"];
        const sub = cat.subs.find(s => s.name.includes("Baby") || s.name.includes("Infant") || s.name.includes("Lactation")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Gentle Infant Nourishment & Calming Comfort" };
    }

    // 11. Ghee & Honey
    if (t.includes("ghee") || t.includes("honey") || t.includes("ghritam") || t.includes("ghrita") || t.includes("sarpis")) {
        const cat = catMap["Ghee & Honey"];
        const sub = cat.subs.find(s => s.name.includes("Ghee") || s.name.includes("Honey") || s.name.includes("Ghrithas")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Ojas Nourishment & Classical Anupana Vehicle" };
    }

    // 12. Massage Oils & Tailas
    if (t.includes("thailam") || t.includes("taila") || t.includes("tel") || t.includes("tail") || t.includes("oil") || t.includes("murivenna") || t.includes("dhanwantharam") || t.includes("ksheerabala") || t.includes("sahacharadi") || t.includes("mahanarayan") || t.includes("pinda") || t.includes("karpooradi") || t.includes("shadbindu") || t.includes("anu tail") || t.includes("jathyadi") || t.includes("kasisadi") || t.includes("panchagun") || t.includes("vishagarbha")) {
        const cat = catMap["Massage Oils & Tailas"];
        const sub = cat.subs.find(s => s.name.includes("Mahanarayan") || s.name.includes("Dhanwantharam") || s.name.includes("Bala")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Neuromuscular Rejuvenation & Abhyanga Harmony" };
    }

    // 13. Herbal Teas & Kadhas
    if (t.includes("tea") || t.includes("kadha") || t.includes("hot sip") || t.includes("infusion") || t.includes("pawani")) {
        const cat = catMap["Herbal Teas & Kadhas"];
        const sub = cat.subs.find(s => s.name.includes("Spiced") || s.name.includes("Tulsi") || s.name.includes("Chamomile")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Agni Kindling & Refreshing Tridoshic Balance" };
    }

    // 14. Digestive Care
    if (t.includes("pudin hara") || t.includes("gasex") || t.includes("herbolax") || t.includes("abhayarishta") || t.includes("drakshashta") || t.includes("kumari asava") || t.includes("kutajarishta") || t.includes("amritarishta") || t.includes("pancharishta") || t.includes("nityam") || t.includes("dizomap") || t.includes("hajmola") || t.includes("hingoli") || t.includes("anardana") || t.includes("nature care") || t.includes("active antacid") || t.includes("kabz-har") || t.includes("kamdudha") || t.includes("sutashekhar") || t.includes("chitrakadi") || t.includes("agnitundi") || t.includes("sanjivani") || t.includes("triphala") || t.includes("avipattikar") || t.includes("hingwashtak") || t.includes("vomiteb") || t.includes("rigestid") || t.includes("dipya") || t.includes("ashtachooranam") || t.includes("hinguvachadi") || t.includes("liv.52") || t.includes("livomyn") || t.includes("livogrit") || t.includes("livomap") || t.includes("pilex") || t.includes("pirand")) {
        const cat = catMap["Digestive Care"];
        const sub = cat.subs.find(s => s.name.includes("Digestive") || s.name.includes("Gut") || s.name.includes("Teas")) || cat.subs[0];
        return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Digestive Agni Harmonization, Gut Health & Gentle Elimination" };
    }

    // 15. Default: Immunity & Rasayana
    const cat = catMap["Immunity & Rasayana"];
    const sub = cat.subs.find(s => s.name.includes("Chyawanprash") || s.name.includes("Tonics") || s.name.includes("Giloy")) || cat.subs[0];
    return { catName: cat.name, catId: cat.id, subName: sub.name, subId: sub.id, subUsage: "Ojas Nourishment, Seasonal Vitality & Immune Resilience" };
}

// Helper to generate slug
function slugify(text) {
    return text.toLowerCase()
        .replace(/[^a-z0-9\s-]/g, '')
        .replace(/[\s-]+/g, '-')
        .replace(/^-+|-+$/g, '');
}

// -----------------------------------------------------------------------------
// 3. PARSE USER 400 PRODUCTS
// -----------------------------------------------------------------------------
const rawLines = fs.readFileSync(USER_REQ_PATH, 'utf8').split(/\r?\n/);
const brandSectionHeaders = {
    "1. HIMALAYA WELLNESS": "Himalaya Wellness",
    "2. DABUR INDIA": "Dabur India",
    "3. PATANJALI AYURVED": "Patanjali Ayurved",
    "4. BAIDYANATH (SHREE BAIDYANATH AYURVED BHAWAN)": "Baidyanath",
    "5. ZANDU CARE (EMAMI)": "Zandu Care",
    "6. KOTTAKKAL ARYA VAIDYA SALA (AVS)": "Kottakkal Arya Vaidya Sala",
    "7. CHARAK PHARMA": "Charak Pharma",
    "8. AVP (THE ARYA VAIDYA PHARMACY - COIMBATORE)": "AVP — The Arya Vaidya Pharmacy",
    "9. MAHARISHI AYURVEDA": "Maharishi Ayurveda",
    "10. SRI SRI TATTVA (ART OF LIVING)": "Sri Sri Tattva"
};

let activeBrand = null;
const parsedList = [];

for (const line of rawLines) {
    const trimmed = line.trim();
    if (trimmed.startsWith('# TASK:')) break;

    for (const [hdr, bName] of Object.entries(brandSectionHeaders)) {
        if (trimmed.startsWith(hdr)) {
            activeBrand = bName;
            break;
        }
    }

    if (activeBrand && trimmed.startsWith('- ')) {
        const itemTitle = trimmed.substring(2).trim();
        parsedList.push({
            brand: activeBrand,
            title: itemTitle
        });
    }
}

console.log(`Parsed ${parsedList.length} products across 10 authentic brands.`);

// -----------------------------------------------------------------------------
// 4. SVG GENERATION ENGINE
// -----------------------------------------------------------------------------
function getArchetype(title) {
    const t = title.toLowerCase();
    if (t.includes("chyawanprash") || t.includes("prash") || t.includes("avaleha") || t.includes("rasayanam") || t.includes("leham") || t.includes("lehya") || t.includes("kalash") || t.includes("pak")) return "jar";
    if (t.includes("ghee") || t.includes("ghritam") || t.includes("ghrita") || t.includes("sarpis") || t.includes("honey")) return "jar";
    if (t.includes("churna") || t.includes("choornam") || t.includes("powder") || t.includes("bhasma") || t.includes("pishti") || t.includes("samagri") || t.includes("grass")) return "powder_jar";
    if (t.includes("thailam") || t.includes("taila") || t.includes("tail") || t.includes("tel") || t.includes("oil") || t.includes("drops") || t.includes("liniment")) return "oil_bottle";
    if (t.includes("syrup") || t.includes("asava") || t.includes("arishta") || t.includes("kwath") || t.includes("kashayam") || t.includes("pravahi") || t.includes("elixir") || t.includes("tonic") || t.includes("cough")) return "syrup_bottle";
    if (t.includes("toothpaste") || t.includes("paste") || t.includes("face wash") || t.includes("shampoo") || t.includes("gel") || t.includes("cream") || t.includes("lotion") || t.includes("scrub") || t.includes("pack")) return "tube";
    if (t.includes("juice") || t.includes("sharbat") || t.includes("drink")) return "juice_bottle";
    if (t.includes("tea")) return "tea_caddy";
    if (t.includes("balm") || t.includes("patch")) return "balm_tin";
    return "tablet_bottle";
}

function generateProductSvg(brandName, productTitle, subtitle = "") {
    const theme = BRAND_CONFIGS[brandName] || BRAND_CONFIGS["Himalaya Wellness"];
    const archetype = getArchetype(productTitle);

    const safeTitle = productTitle.replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
    const safeBrand = brandName.toUpperCase().replace(/&/g, "&amp;");
    const safeSubtitle = subtitle ? subtitle.toUpperCase().replace(/&/g, "&amp;") : "AUTHENTIC AYURVEDIC FORMULATION";

    let titleLine1 = safeTitle;
    let titleLine2 = "";
    if (safeTitle.length > 24) {
        const words = safeTitle.split(' ');
        titleLine1 = words.slice(0, Math.ceil(words.length / 2)).join(' ');
        titleLine2 = words.slice(Math.ceil(words.length / 2)).join(' ');
    }

    let containerSvg = "";

    if (archetype === "syrup_bottle" || archetype === "juice_bottle") {
        containerSvg = `
          <ellipse cx="300" cy="510" rx="170" ry="22" fill="url(#podiumShadow)" />
          <ellipse cx="300" cy="502" rx="140" ry="12" fill="#E8E2D8" />
          <rect x="225" y="220" width="150" height="260" rx="16" fill="url(#bottleGrad)" />
          <path d="M225 240 C225 180 265 170 265 145 L335 145 C335 170 375 180 375 240 Z" fill="url(#bottleGrad)" />
          <rect x="270" y="125" width="60" height="24" fill="url(#bottleGrad)" />
          <rect x="262" y="90" width="76" height="38" rx="5" fill="url(#capGrad)" stroke="${theme.accent}" stroke-width="1.5" />
          <line x1="262" y1="102" x2="338" y2="102" stroke="rgba(255,255,255,0.4)" stroke-width="1.5" />
          <rect x="233" y="240" width="134" height="200" rx="6" fill="${theme.labelBg}" stroke="${theme.primary}" stroke-width="1" />
          <rect x="237" y="245" width="126" height="34" rx="3" fill="${theme.headerBg}" />
          <text x="300" y="267" font-family="'Segoe UI', Roboto, sans-serif" font-size="11" font-weight="bold" fill="${theme.headerText}" text-anchor="middle" letter-spacing="0.5">${safeBrand}</text>
          <text x="300" y="305" font-family="'Georgia', serif" font-size="${titleLine2 ? 15 : 17}" font-weight="bold" fill="${theme.primary}" text-anchor="middle">${titleLine1}</text>
          ${titleLine2 ? `<text x="300" y="324" font-family="'Georgia', serif" font-size="15" font-weight="bold" fill="${theme.primary}" text-anchor="middle">${titleLine2}</text>` : ''}
          <text x="300" y="${titleLine2 ? 344 : 330}" font-family="'Segoe UI', sans-serif" font-size="9" fill="#6B5B45" text-anchor="middle" letter-spacing="0.5">${safeSubtitle}</text>
          <circle cx="300" cy="${titleLine2 ? 375 : 365}" r="16" fill="#F0EBE1" stroke="${theme.accent}" stroke-width="1.5" />
          <path d="M300 ${titleLine2 ? 364 : 354} Q307 ${titleLine2 ? 374 : 364} 300 ${titleLine2 ? 385 : 375} Q293 ${titleLine2 ? 374 : 364} 300 ${titleLine2 ? 364 : 354} Z" fill="${theme.primary}" />
          <rect x="248" y="405" width="104" height="22" rx="11" fill="#EFE8DA" stroke="${theme.primary}" stroke-width="0.75" />
          <text x="300" y="420" font-family="'Segoe UI', sans-serif" font-size="8.5" font-weight="bold" fill="${theme.primary}" text-anchor="middle">${theme.sealText}</text>
        `;
    } else if (archetype === "jar" || archetype === "powder_jar") {
        containerSvg = `
          <ellipse cx="300" cy="510" rx="190" ry="24" fill="url(#podiumShadow)" />
          <ellipse cx="300" cy="502" rx="160" ry="14" fill="#E8E2D8" />
          <rect x="205" y="230" width="190" height="250" rx="20" fill="url(#bottleGrad)" />
          <rect x="195" y="195" width="210" height="42" rx="8" fill="url(#capGrad)" stroke="${theme.accent}" stroke-width="2" />
          <line x1="195" y1="210" x2="405" y2="210" stroke="rgba(255,255,255,0.4)" stroke-width="2" />
          <rect x="215" y="255" width="170" height="195" rx="8" fill="${theme.labelBg}" stroke="${theme.accent}" stroke-width="1.5" />
          <rect x="221" y="261" width="158" height="34" rx="4" fill="${theme.headerBg}" />
          <text x="300" y="283" font-family="'Segoe UI', Roboto, sans-serif" font-size="12" font-weight="bold" fill="${theme.headerText}" text-anchor="middle" letter-spacing="0.5">${safeBrand}</text>
          <text x="300" y="322" font-family="'Georgia', serif" font-size="${titleLine2 ? 16 : 18}" font-weight="bold" fill="${theme.primary}" text-anchor="middle">${titleLine1}</text>
          ${titleLine2 ? `<text x="300" y="342" font-family="'Georgia', serif" font-size="16" font-weight="bold" fill="${theme.primary}" text-anchor="middle">${titleLine2}</text>` : ''}
          <text x="300" y="${titleLine2 ? 362 : 348}" font-family="'Segoe UI', sans-serif" font-size="9.5" fill="#6B5B45" text-anchor="middle" letter-spacing="0.5">${safeSubtitle}</text>
          <circle cx="300" cy="${titleLine2 ? 392 : 382}" r="17" fill="#F0EBE1" stroke="${theme.accent}" stroke-width="1.5" />
          <path d="M300 ${titleLine2 ? 380 : 370} Q308 ${titleLine2 ? 391 : 381} 300 ${titleLine2 ? 403 : 393} Q292 ${titleLine2 ? 391 : 381} 300 ${titleLine2 ? 380 : 370} Z" fill="${theme.primary}" />
          <rect x="235" y="418" width="130" height="22" rx="11" fill="#EFE8DA" stroke="${theme.primary}" stroke-width="0.75" />
          <text x="300" y="433" font-family="'Segoe UI', sans-serif" font-size="9" font-weight="bold" fill="${theme.primary}" text-anchor="middle">${theme.sealText}</text>
        `;
    } else if (archetype === "tube") {
        containerSvg = `
          <ellipse cx="300" cy="510" rx="160" ry="20" fill="url(#podiumShadow)" />
          <ellipse cx="300" cy="502" rx="130" ry="12" fill="#E8E2D8" />
          <rect x="250" y="460" width="100" height="35" rx="5" fill="url(#capGrad)" stroke="${theme.accent}" stroke-width="1.5" />
          <path d="M255 460 L230 200 L370 200 L345 460 Z" fill="${theme.labelBg}" stroke="${theme.primary}" stroke-width="1.5" />
          <rect x="225" y="180" width="150" height="22" rx="2" fill="${theme.headerBg}" stroke="${theme.accent}" stroke-width="1" />
          <line x1="225" y1="188" x2="375" y2="188" stroke="rgba(255,255,255,0.4)" stroke-width="1.5" />
          <rect x="240" y="220" width="120" height="30" rx="4" fill="${theme.headerBg}" />
          <text x="300" y="240" font-family="'Segoe UI', Roboto, sans-serif" font-size="11" font-weight="bold" fill="${theme.headerText}" text-anchor="middle">${safeBrand}</text>
          <text x="300" y="285" font-family="'Georgia', serif" font-size="${titleLine2 ? 15 : 17}" font-weight="bold" fill="${theme.primary}" text-anchor="middle">${titleLine1}</text>
          ${titleLine2 ? `<text x="300" y="305" font-family="'Georgia', serif" font-size="15" font-weight="bold" fill="${theme.primary}" text-anchor="middle">${titleLine2}</text>` : ''}
          <text x="300" y="${titleLine2 ? 325 : 310}" font-family="'Segoe UI', sans-serif" font-size="9" fill="#6B5B45" text-anchor="middle">${safeSubtitle}</text>
          <circle cx="300" cy="360" r="22" fill="#F0EBE1" stroke="${theme.accent}" stroke-width="1.5" />
          <path d="M300 345 Q310 360 300 375 Q290 360 300 345 Z" fill="${theme.primary}" />
          <rect x="245" y="415" width="110" height="22" rx="11" fill="#EFE8DA" stroke="${theme.primary}" stroke-width="0.75" />
          <text x="300" y="430" font-family="'Segoe UI', sans-serif" font-size="8.5" font-weight="bold" fill="${theme.primary}" text-anchor="middle">${theme.sealText}</text>
        `;
    } else {
        // Default: Tablet / Capsules Bottle
        containerSvg = `
          <ellipse cx="300" cy="510" rx="170" ry="22" fill="url(#podiumShadow)" />
          <ellipse cx="300" cy="502" rx="140" ry="12" fill="#E8E2D8" />
          <rect x="225" y="215" width="150" height="265" rx="18" fill="url(#bottleGrad)" />
          <path d="M225 235 C225 185 260 175 260 155 L340 155 C340 175 375 185 375 235 Z" fill="url(#bottleGrad)" />
          <rect x="265" y="135" width="70" height="24" fill="url(#bottleGrad)" />
          <rect x="256" y="102" width="88" height="38" rx="6" fill="url(#capGrad)" stroke="${theme.accent}" stroke-width="1.5" />
          <line x1="256" y1="115" x2="344" y2="115" stroke="rgba(255,255,255,0.4)" stroke-width="1.5" />
          <rect x="233" y="235" width="134" height="210" rx="6" fill="${theme.labelBg}" stroke="${theme.accent}" stroke-width="1" />
          <rect x="237" y="240" width="126" height="34" rx="3" fill="${theme.headerBg}" />
          <text x="300" y="262" font-family="'Segoe UI', Roboto, sans-serif" font-size="11" font-weight="bold" fill="${theme.headerText}" text-anchor="middle" letter-spacing="0.5">${safeBrand}</text>
          <text x="300" y="302" font-family="'Georgia', serif" font-size="${titleLine2 ? 15 : 17}" font-weight="bold" fill="${theme.primary}" text-anchor="middle">${titleLine1}</text>
          ${titleLine2 ? `<text x="300" y="322" font-family="'Georgia', serif" font-size="15" font-weight="bold" fill="${theme.primary}" text-anchor="middle">${titleLine2}</text>` : ''}
          <text x="300" y="${titleLine2 ? 342 : 328}" font-family="'Segoe UI', sans-serif" font-size="9" fill="#6B5B45" text-anchor="middle" letter-spacing="0.5">${safeSubtitle}</text>
          <circle cx="300" cy="${titleLine2 ? 375 : 365}" r="17" fill="#F0EBE1" stroke="${theme.accent}" stroke-width="1.5" />
          <path d="M300 ${titleLine2 ? 364 : 354} Q308 ${titleLine2 ? 375 : 365} 300 ${titleLine2 ? 386 : 376} Q292 ${titleLine2 ? 375 : 365} 300 ${titleLine2 ? 364 : 354} Z" fill="${theme.primary}" />
          <rect x="246" y="412" width="108" height="22" rx="11" fill="#EFE8DA" stroke="${theme.primary}" stroke-width="0.75" />
          <text x="300" y="427" font-family="'Segoe UI', sans-serif" font-size="8.5" font-weight="bold" fill="${theme.primary}" text-anchor="middle">${theme.sealText}</text>
        `;
    }

    return `
<svg width="600" height="600" viewBox="0 0 600 600" xmlns="http://www.w3.org/2000/svg">
  <defs>
    <radialGradient id="bgGlow" cx="50%" cy="45%" r="65%">
      <stop offset="0%" stop-color="#FFFFFF" />
      <stop offset="60%" stop-color="#FBF9F5" />
      <stop offset="100%" stop-color="#EFECE6" />
    </radialGradient>
    <radialGradient id="podiumShadow" cx="50%" cy="50%" r="50%">
      <stop offset="0%" stop-color="rgba(30,40,35,0.22)" />
      <stop offset="60%" stop-color="rgba(30,40,35,0.07)" />
      <stop offset="100%" stop-color="rgba(30,40,35,0)" />
    </radialGradient>
    <linearGradient id="bottleGrad" x1="0%" y1="0%" x2="100%" y2="0%">
      <stop offset="0%" stop-color="${theme.bottleColor}" />
      <stop offset="30%" stop-color="#3A4A3E" />
      <stop offset="50%" stop-color="#4F6455" />
      <stop offset="70%" stop-color="#3A4A3E" />
      <stop offset="100%" stop-color="${theme.bottleColor}" />
    </linearGradient>
    <linearGradient id="capGrad" x1="0%" y1="0%" x2="100%" y2="0%">
      <stop offset="0%" stop-color="#9A7B38" />
      <stop offset="50%" stop-color="${theme.accent}" />
      <stop offset="100%" stop-color="#735A22" />
    </linearGradient>
  </defs>
  <rect width="600" height="600" fill="url(#bgGlow)" />
  ${containerSvg}
</svg>
`;
}

// -----------------------------------------------------------------------------
// 5. ASSEMBLE 400 MASTER CATALOG
// -----------------------------------------------------------------------------
async function buildCatalogAndImages() {
    console.log("Starting master catalog enrichment & image generation...");

    const masterCatalog = [];
    const brandIndexCounters = {};

    for (let i = 0; i < parsedList.length; i++) {
        const item = parsedList[i];
        const brand = item.brand;
        const bCfg = BRAND_CONFIGS[brand];
        
        brandIndexCounters[brand] = (brandIndexCounters[brand] || 0) + 1;
        const bIdx = brandIndexCounters[brand];

        const classification = classifyProduct(item.title);
        const productSlug = slugify(item.title);
        const imgFilename = `${bCfg.slug}-${productSlug}.webp`;
        const imgUrl = `/images/products/${imgFilename}`;
        const sku = `TGA-${bCfg.code}-${String(bIdx).padStart(3, '0')}`;

        // Standard realistic Ayurvedic pricing (INR 140 to 650)
        const basePrices = [185, 220, 260, 295, 340, 395, 450, 520, 580, 650];
        const price = basePrices[(i * 3 + bIdx) % basePrices.length];
        const mrp = Math.round(price * 1.25);
        const rating = Number((4.6 + ((i % 4) * 0.1)).toFixed(1));
        const stock = (i % 15 === 0) ? 0 : ((i % 7 === 0) ? 3 : (12 + (i % 25)));

        // Description, Ingredients, TraditionallyUsedFor, HowToUse, Precautions
        const description = `Authentic ${item.title} formulated by ${brand} adhering strictly to Classical Ayurvedic pharmacopoeia. Enriched with standardized botanical extracts to naturally nurture Ahara-Vihara balance, promote metabolic stability, and support daily vitality.`;
        const traditionallyUsedFor = classification.subUsage;
        const ingredients = `Standardized Ayurvedic extracts of Classical herbs, wildcrafted forest botanicals, natural minerals, and traditional essential extracts.`;
        const howToUse = `As traditionally recommended: Take 1 to 2 tablets or 1 to 2 teaspoons twice daily with lukewarm water or warm milk after meals, or as directed by an Ayurvedic physician.`;
        const precautions = `Store in a cool, dry place away from direct sunlight. Close container securely after use. Consult a Vaidya if pregnant or lactating.`;

        // 3 Variants
        const variants = [
            {
                title: "Standard Pack",
                sizeOrPack: "60 Tablets / 100ml / 100g",
                price: price,
                mrp: mrp,
                stock: stock
            },
            {
                title: "Economy Value Pack",
                sizeOrPack: "120 Tablets / 200ml / 250g",
                price: Math.round(price * 1.8),
                mrp: Math.round(mrp * 1.8),
                stock: stock > 0 ? stock + 5 : 0
            },
            {
                title: "Family Practitioner Pack",
                sizeOrPack: "240 Tablets / 500ml / 500g",
                price: Math.round(price * 3.2),
                mrp: Math.round(mrp * 3.2),
                stock: stock > 0 ? stock + 2 : 0
            }
        ];

        // Reviews
        const reviews = [
            {
                rating: 5,
                text: `Exceptional traditional quality from ${brand}. Noticed genuine wellness results within two weeks of regular use.`,
                helpful: 12 + (i % 10),
                daysAgo: 5 + (i % 20)
            },
            {
                rating: 4,
                text: "Prompt delivery and very clean packaging. Highly authentic formulation.",
                helpful: 6 + (i % 8),
                daysAgo: 14 + (i % 15)
            }
        ];

        // FAQs
        const faqs = [
            {
                question: `What is the optimal time to take ${item.title}?`,
                answer: `Traditional Ayurvedic protocols recommend taking this after breakfast and dinner with warm water or as recommended by your Vaidya.`,
                askedBy: "Priya S."
            }
        ];

        const productObj = {
            title: item.title,
            brand: brand,
            categoryName: classification.catName,
            subcategoryName: classification.subName,
            description: description,
            price: price,
            mrp: mrp,
            stock: stock,
            rating: rating,
            imageUrl: imgUrl,
            sku: sku,
            tags: `${classification.catName.toLowerCase()}, ${classification.subName.toLowerCase()}, ${brand.toLowerCase()}, ayurvedic, authentic`,
            traditionallyUsedFor: traditionallyUsedFor,
            ingredients: ingredients,
            howToUse: howToUse,
            precautions: precautions,
            isPrescriptionRequired: false,
            variants: variants,
            reviews: reviews,
            faqs: faqs
        };

        masterCatalog.push(productObj);

        // Generate the 600x600 WebP Image
        const svg = generateProductSvg(brand, item.title, classification.subUsage);
        const outImgPath = path.join(OUTPUT_IMG_DIR, imgFilename);
        await sharp(Buffer.from(svg))
            .webp({ quality: 90 })
            .toFile(outImgPath);

        // Check if item has explicit variants in title, e.g. "(Tablet / Syrup)"
        if (item.title.includes("(") && item.title.includes("/")) {
            const inner = item.title.substring(item.title.indexOf("(") + 1, item.title.indexOf(")"));
            const subVariants = inner.split("/").map(s => s.trim());
            for (const subV of subVariants) {
                const subTitle = `${item.title.split("(")[0].trim()} ${subV}`;
                const subSlug = slugify(subTitle);
                const subFilename = `${bCfg.slug}-${subSlug}.webp`;
                const subOutPath = path.join(OUTPUT_IMG_DIR, subFilename);
                const subSvg = generateProductSvg(brand, subTitle, `${subV.toUpperCase()} FORMULATION`);
                await sharp(Buffer.from(subSvg))
                    .webp({ quality: 90 })
                    .toFile(subOutPath);
            }
        }

        if ((i + 1) % 50 === 0 || (i + 1) === parsedList.length) {
            console.log(`Progress: [${i + 1}/${parsedList.length}] images generated & catalog items prepared.`);
        }
    }

    // Save master catalog to catalog_300plus.json
    fs.writeFileSync(CATALOG_OUTPUT_PATH, JSON.stringify(masterCatalog, null, 2), 'utf8');
    console.log(`\nSuccessfully saved ${masterCatalog.length} rich Ayurvedic products to: ${CATALOG_OUTPUT_PATH}`);
}

buildCatalogAndImages().catch(err => {
    console.error("Fatal error:", err);
    process.exit(1);
});
