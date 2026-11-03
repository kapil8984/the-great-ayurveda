const fs = require('fs');
const path = require('path');
const sharp = require('../tests/e2e/node_modules/sharp');

const REPO_ROOT = path.resolve(__dirname, '..');
const CATALOG_PATH = path.join(REPO_ROOT, 'src/Ayurveda.Infrastructure/Data/catalog_300plus.json');
const OUTPUT_DIR = path.join(REPO_ROOT, 'src/Ayurveda.Web/wwwroot/images/products');

if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

const products = JSON.parse(fs.readFileSync(CATALOG_PATH, 'utf8'));
console.log(`Loaded ${products.length} products from catalog_300plus.json.`);

// -----------------------------------------------------------------------------
// DIVERSITY MATRICES & COLOR PALETTES
// -----------------------------------------------------------------------------
const BRAND_HUES = [
    { name: "Forest Herb", h1: 148, s1: 35, l1: 94, h2: 152, s2: 45, l2: 84, accent: "#1F5E3B" },
    { name: "Warm Gold", h1: 42, s1: 50, l1: 95, h2: 38, s2: 60, l2: 82, accent: "#E3A72F" },
    { name: "Soft Sage", h1: 135, s1: 25, l1: 93, h2: 130, s2: 32, l2: 80, accent: "#3B6E4A" },
    { name: "Cream Linen", h1: 45, s1: 30, l1: 96, h2: 40, s2: 25, l2: 86, accent: "#8B6B38" },
    { name: "Terracotta Earth", h1: 16, s1: 45, l1: 94, h2: 14, s2: 50, l2: 82, accent: "#A4553A" },
    { name: "Warm Clay", h1: 22, s1: 35, l1: 93, h2: 20, s2: 40, l2: 80, accent: "#8A4E38" },
    { name: "Deep Amber", h1: 32, s1: 55, l1: 95, h2: 28, s2: 65, l2: 78, accent: "#C27A1A" },
    { name: "Mineral Slate", h1: 165, s1: 18, l1: 93, h2: 170, s2: 22, l2: 82, accent: "#2D4B3E" }
];

function getArchetype(p) {
    const title = (p.title || "").toLowerCase();
    const sub = (p.subcategoryName || "").toLowerCase();
    const cat = (p.categoryName || "").toLowerCase();

    if (sub.includes("chyawanprash") || title.includes("chyawanprash") || sub.includes("balm") || title.includes("balm")) {
        return "amber_jar";
    }
    if (sub.includes("taila") || sub.includes("oil") || title.includes("oil") || sub.includes("serum") || title.includes("drops") || title.includes("thailam")) {
        return "oil_dropper";
    }
    if (sub.includes("churna") || sub.includes("powder") || title.includes("churna") || title.includes("powder") || sub.includes("ubtan") || title.includes("ubtan")) {
        return "powder_bowl";
    }
    if (sub.includes("tea") || sub.includes("kadha") || title.includes("tea") || title.includes("infusion")) {
        return "tea_cup";
    }
    if (sub.includes("ghee") || title.includes("ghee") || sub.includes("ghrith")) {
        return "ghee_pot";
    }
    if (sub.includes("honey") || title.includes("honey")) {
        return "honey_jar";
    }
    if (sub.includes("soap") || title.includes("bar") || title.includes("soap")) {
        return "soap_bar";
    }
    if (sub.includes("tablet") || sub.includes("vati") || title.includes("vati") || title.includes("tablets") || title.includes("capsules") || title.includes("resin") || title.includes("shilajit")) {
        return "tablet_bowl";
    }
    if (sub.includes("asava") || sub.includes("tonic") || sub.includes("cleanser") || sub.includes("shampoo") || title.includes("kwath") || title.includes("elixir") || title.includes("shampoo")) {
        return "elixir_bottle";
    }
    return "amber_jar";
}

function buildSvgScene(p, idx, variantSeed = 0) {
    const seed = idx + variantSeed * 313;
    const palette = BRAND_HUES[(seed * 5) % BRAND_HUES.length];
    const archetype = getArchetype(p);

    // Asymmetric lighting angles
    const lightAngle = (seed * 47) % 360;
    const rad = (lightAngle * Math.PI) / 180;
    const lx1 = Math.round(50 + 45 * Math.cos(rad));
    const ly1 = Math.round(50 + 45 * Math.sin(rad));
    const lx2 = Math.round(50 - 45 * Math.cos(rad));
    const ly2 = Math.round(50 - 45 * Math.sin(rad));

    // Dynamic placement & framing
    const shiftX = ((seed * 17) % 50) - 25; // -25 to +25
    const shiftY = ((seed * 19) % 30) - 15; // -15 to +15
    const rotObj = ((seed * 7) % 11) - 5;   // -5 to +5 deg
    const objScale = 0.92 + ((seed % 7) * 0.025); // 0.92 to 1.07

    const cx = 300 + shiftX;
    const cy = 340 + shiftY;

    // Podium variation
    const podiumType = seed % 6;
    let podiumSvg = "";
    if (podiumType === 0) {
        // Sandstone circular pedestal
        podiumSvg = `
            <ellipse cx="${cx}" cy="${cy + 105}" rx="180" ry="42" fill="#BFAF9B" opacity="0.45" />
            <path d="M${cx - 170} ${cy + 95} L${cx + 170} ${cy + 95} L${cx + 165} ${cy + 120} L${cx - 165} ${cy + 120} Z" fill="#C5B6A3" />
            <ellipse cx="${cx}" cy="${cy + 95}" rx="170" ry="36" fill="#DDD1BF" />
            <ellipse cx="${cx}" cy="${cy + 92}" rx="166" ry="33" fill="#EFE8DD" />
        `;
    } else if (podiumType === 1) {
        // Teak wood disc with grain
        podiumSvg = `
            <ellipse cx="${cx}" cy="${cy + 110}" rx="185" ry="38" fill="#362215" opacity="0.4" />
            <path d="M${cx - 160} ${cy + 95} L${cx + 160} ${cy + 95} L${cx + 155} ${cy + 122} L${cx - 155} ${cy + 122} Z" fill="#694429" />
            <ellipse cx="${cx}" cy="${cy + 95}" rx="160" ry="32" fill="#8C5C38" />
            <ellipse cx="${cx}" cy="${cy + 92}" rx="156" ry="30" fill="#A46E44" />
            <ellipse cx="${cx - 20}" cy="${cy + 92}" rx="110" ry="20" fill="none" stroke="#7E4E2C" stroke-width="1.5" opacity="0.6" />
        `;
    } else if (podiumType === 2) {
        // Natural Dark Slate
        podiumSvg = `
            <ellipse cx="${cx}" cy="${cy + 108}" rx="180" ry="36" fill="#151C1A" opacity="0.45" />
            <path d="M${cx - 165} ${cy + 95} L${cx + 165} ${cy + 95} L${cx + 160} ${cy + 118} L${cx - 160} ${cy + 118} Z" fill="#293330" />
            <ellipse cx="${cx}" cy="${cy + 95}" rx="165" ry="30" fill="#3D4A46" />
            <ellipse cx="${cx}" cy="${cy + 93}" rx="160" ry="28" fill="#4D5D58" />
        `;
    } else if (podiumType === 3) {
        // Travertine block
        podiumSvg = `
            <ellipse cx="${cx}" cy="${cy + 112}" rx="175" ry="38" fill="#AFA598" opacity="0.4" />
            <rect x="${cx - 150}" y="${cy + 92}" width="300" height="28" rx="6" fill="#CDC3B5" />
            <ellipse cx="${cx}" cy="${cy + 92}" rx="150" ry="28" fill="#E4DCD0" />
            <ellipse cx="${cx}" cy="${cy + 90}" rx="146" ry="26" fill="#F4EDE3" />
        `;
    } else if (podiumType === 4) {
        // Terracotta / Ceramic Plate
        podiumSvg = `
            <ellipse cx="${cx}" cy="${cy + 106}" rx="190" ry="42" fill="#5E331B" opacity="0.35" />
            <ellipse cx="${cx}" cy="${cy + 95}" rx="180" ry="35" fill="#8F512E" />
            <ellipse cx="${cx}" cy="${cy + 92}" rx="172" ry="31" fill="#B36B40" />
            <ellipse cx="${cx}" cy="${cy + 90}" rx="164" ry="28" fill="#CC8154" />
        `;
    } else {
        // Minimal Floating Studio Surface
        podiumSvg = `
            <ellipse cx="${cx}" cy="${cy + 104}" rx="195" ry="44" fill="#000000" opacity="0.18" />
            <ellipse cx="${cx}" cy="${cy + 94}" rx="185" ry="36" fill="#DFD7CB" />
            <ellipse cx="${cx}" cy="${cy + 90}" rx="180" ry="33" fill="#F1ECE2" />
        `;
    }

    // Hero Object (strictly NO text, NO logos, NO labels)
    let heroObject = "";
    if (archetype === "amber_jar") {
        heroObject = `
            <!-- Contact Shadow -->
            <ellipse cx="${cx}" cy="${cy + 90}" rx="92" ry="18" fill="#000000" opacity="0.4" />
            <!-- Jar Body -->
            <path d="M${cx - 85} ${cy - 100} L${cx + 85} ${cy - 100} Q${cx + 95} ${cy - 100} ${cx + 95} ${cy - 88} L${cx + 95} ${cy + 75} Q${cx + 95} ${cy + 92} ${cx + 75} ${cy + 94} L${cx - 75} ${cy + 94} Q${cx - 95} ${cy + 92} ${cx - 95} ${cy + 75} L${cx - 95} ${cy - 88} Q${cx - 95} ${cy - 100} ${cx - 85} ${cy - 100} Z" fill="url(#jarGrad_${seed})" filter="url(#dropShadow)" />
            <!-- Rich Ayurvedic Resin Fill -->
            <rect x="${cx - 82}" y="${cy - 82}" width="164" height="162" rx="10" fill="#240F04" opacity="0.88" />
            <!-- Glass Specular Reflection -->
            <rect x="${cx - 80}" y="${cy - 80}" width="14" height="158" rx="6" fill="#FFFFFF" opacity="0.28" />
            <!-- Jar Neck & Brass Cap -->
            <rect x="${cx - 60}" y="${cy - 120}" width="120" height="22" rx="3" fill="#3D1A05" />
            <rect x="${cx - 72}" y="${cy - 142}" width="144" height="24" rx="5" fill="url(#brassCap_${seed})" />
            <ellipse cx="${cx}" cy="${cy - 142}" rx="72" ry="12" fill="#E5B250" />
            <ellipse cx="${cx}" cy="${cy - 144}" rx="68" ry="10" fill="#F7CA6E" opacity="0.75" />
        `;
    } else if (archetype === "oil_dropper") {
        heroObject = `
            <ellipse cx="${cx}" cy="${cy + 90}" rx="75" ry="16" fill="#000000" opacity="0.38" />
            <!-- Glass Dropper Bottle -->
            <path d="M${cx - 65} ${cy - 70} L${cx + 65} ${cy - 70} Q${cx + 72} ${cy - 70} ${cx + 72} ${cy - 55} L${cx + 72} ${cy + 75} Q${cx + 72} ${cy + 92} ${cx + 55} ${cy + 94} L${cx - 55} ${cy + 94} Q${cx - 72} ${cy + 92} ${cx - 72} ${cy + 75} L${cx - 72} ${cy - 55} Q${cx - 72} ${cy - 70} ${cx - 65} ${cy - 70} Z" fill="url(#dropperGrad_${seed})" filter="url(#dropShadow)" />
            <!-- Translucent Oil Fill -->
            <rect x="${cx - 62}" y="${cy - 52}" width="124" height="134" rx="8" fill="#1C3822" opacity="0.85" />
            <rect x="${cx - 60}" y="${cy - 55}" width="10" height="138" rx="4" fill="#FFFFFF" opacity="0.35" />
            <!-- Shoulder & Collar -->
            <path d="M${cx - 40} ${cy - 105} L${cx + 40} ${cy - 105} L${cx + 65} ${cy - 70} L${cx - 65} ${cy - 70} Z" fill="#14281A" />
            <rect x="${cx - 28}" y="${cy - 135}" width="56" height="32" rx="3" fill="url(#brassCap_${seed})" />
            <!-- Pipette Bulb -->
            <path d="M${cx - 24} ${cy - 135} C${cx - 24} ${cy - 170} ${cx + 24} ${cy - 170} ${cx + 24} ${cy - 135} Z" fill="#222222" />
            <!-- Suspended Droplet -->
            <ellipse cx="${cx + 45}" cy="${cy + 82}" rx="7" ry="12" fill="#E8AA3D" opacity="0.9" />
            <circle cx="${cx + 43}" cy="${cy + 78}" r="2.5" fill="#FFFFFF" opacity="0.6" />
        `;
    } else if (archetype === "powder_bowl") {
        heroObject = `
            <ellipse cx="${cx}" cy="${cy + 92}" rx="115" ry="24" fill="#000000" opacity="0.34" />
            <!-- Ceramic / Earthen Bowl -->
            <path d="M${cx - 105} ${cy - 20} C${cx - 105} ${cy + 95} ${cx + 105} ${cy + 95} ${cx + 105} ${cy - 20} Z" fill="url(#potGrad_${seed})" filter="url(#dropShadow)" />
            <ellipse cx="${cx}" cy="${cy - 20}" rx="105" ry="24" fill="#6A3D1E" />
            <ellipse cx="${cx}" cy="${cy - 22}" rx="100" ry="21" fill="#4E2B13" />
            <!-- Powder Mound -->
            <ellipse cx="${cx}" cy="${cy - 22}" rx="94" ry="19" fill="#B38A4B" />
            <path d="M${cx - 75} ${cy - 20} Q${cx} ${cy - 85} ${cx + 75} ${cy - 20} Z" fill="url(#powderGrad_${seed})" />
            <ellipse cx="${cx}" cy="${cy - 52}" rx="32" ry="14" fill="#D6AA5F" opacity="0.6" />
            <!-- Wooden Scoop -->
            <path d="M${cx + 40} ${cy - 70} L${cx + 105} ${cy + 45} Q${cx + 115} ${cy + 60} ${cx + 102} ${cy + 64} L${cx + 85} ${cy + 60} Z" fill="#9C6B3E" stroke="#5E3B1B" stroke-width="1.5" />
            <ellipse cx="${cx + 36}" cy="${cy - 74}" rx="16" ry="10" fill="#B8834E" />
        `;
    } else if (archetype === "tea_cup") {
        heroObject = `
            <ellipse cx="${cx}" cy="${cy + 90}" rx="92" ry="18" fill="#000000" opacity="0.3" />
            <!-- Handcrafted Tea Cup -->
            <path d="M${cx - 75} ${cy - 50} L${cx - 58} ${cy + 75} Q${cx - 55} ${cy + 88} ${cx - 40} ${cy + 88} L${cx + 40} ${cy + 88} Q${cx + 55} ${cy + 88} ${cx + 58} ${cy + 75} L${cx + 75} ${cy - 50} Z" fill="url(#cupGrad_${seed})" filter="url(#dropShadow)" />
            <ellipse cx="${cx}" cy="${cy - 50}" rx="75" ry="18" fill="#B2A392" />
            <ellipse cx="${cx}" cy="${cy - 52}" rx="70" ry="16" fill="#8C7D6C" />
            <!-- Translucent Brew Liquid -->
            <ellipse cx="${cx}" cy="${cy - 48}" rx="66" ry="14" fill="url(#brewGrad_${seed})" />
            <ellipse cx="${cx + 12}" cy="${cy - 49}" rx="12" ry="3" fill="#5A2E12" />
            <!-- Steam -->
            <path d="M${cx - 15} ${cy - 85} Q${cx - 25} ${cy - 125} ${cx - 10} ${cy - 155}" stroke="#FFFFFF" stroke-width="2.5" fill="none" opacity="0.35" stroke-linecap="round" />
            <path d="M${cx + 15} ${cy - 90} Q${cx + 30} ${cy - 130} ${cx + 15} ${cy - 165}" stroke="#FFFFFF" stroke-width="2" fill="none" opacity="0.28" stroke-linecap="round" />
        `;
    } else if (archetype === "ghee_pot") {
        heroObject = `
            <ellipse cx="${cx}" cy="${cy + 92}" rx="96" ry="20" fill="#000000" opacity="0.35" />
            <!-- Martaban Vessel -->
            <path d="M${cx - 80} ${cy - 30} C${cx - 118} ${cy + 40} ${cx - 75} ${cy + 90} ${cx} ${cy + 90} C${cx + 75} ${cy + 90} ${cx + 118} ${cy + 40} ${cx + 80} ${cy - 30} C${cx + 62} ${cy - 65} ${cx + 45} ${cy - 75} ${cx} ${cy - 75} C${cx - 45} ${cy - 75} ${cx - 62} ${cy - 65} ${cx - 80} ${cy - 30} Z" fill="url(#martabanGrad_${seed})" filter="url(#dropShadow)" />
            <path d="M${cx - 65} ${cy - 55} C${cx - 50} ${cy - 70} ${cx} ${cy - 72} ${cx + 50} ${cy - 70} L${cx + 42} ${cy - 10} C${cx} ${cy - 5} ${cx - 42} ${cy - 10} ${cx - 65} ${cy - 55} Z" fill="#99582A" />
            <!-- Neck & String Tie -->
            <ellipse cx="${cx}" cy="${cy - 76}" rx="46" ry="12" fill="#D38C48" />
            <rect x="${cx - 44}" y="${cy - 86}" width="88" height="12" rx="3" fill="#FAF6EE" />
            <path d="M${cx - 42} ${cy - 74} Q${cx} ${cy - 70} ${cx + 42} ${cy - 74}" stroke="#C9A364" stroke-width="3" fill="none" />
            <path d="M${cx + 18} ${cy - 72} L${cx + 26} ${cy - 45}" stroke="#C9A364" stroke-width="2.5" fill="none" />
        `;
    } else if (archetype === "honey_jar") {
        heroObject = `
            <ellipse cx="${cx}" cy="${cy + 90}" rx="86" ry="18" fill="#000000" opacity="0.34" />
            <!-- Fluted Glass Honey Jar -->
            <path d="M${cx - 70} ${cy - 75} L${cx + 70} ${cy - 75} Q${cx + 78} ${cy - 75} ${cx + 78} ${cy - 60} L${cx + 78} ${cy + 75} Q${cx + 78} ${cy + 92} ${cx + 60} ${cy + 94} L${cx - 60} ${cy + 94} Q${cx - 78} ${cy + 92} ${cx - 78} ${cy + 75} L${cx - 78} ${cy - 60} Q${cx - 78} ${cy - 75} ${cx - 70} ${cy - 75} Z" fill="url(#honeyJarGrad_${seed})" filter="url(#dropShadow)" />
            <rect x="${cx - 70}" y="${cy - 60}" width="140" height="145" rx="8" fill="url(#pureHoneyGrad_${seed})" opacity="0.9" />
            <ellipse cx="${cx}" cy="${cy - 56}" rx="60" ry="8" fill="#FFEAA7" opacity="0.45" />
            <rect x="${cx - 46}" y="${cy - 100}" width="92" height="26" rx="5" fill="#B8864E" stroke="#7A5323" stroke-width="1.5" />
            <!-- Turned Honey Dipper -->
            <path d="M${cx + 55} ${cy - 30} L${cx + 105} ${cy + 85}" stroke="#B07D48" stroke-width="6.5" stroke-linecap="round" />
            <ellipse cx="${cx + 62}" cy="${cy - 15}" rx="11" ry="6" fill="#8F5F2B" transform="rotate(25 ${cx + 62} ${cy - 15})" />
            <ellipse cx="${cx + 68}" cy="${cy}" rx="13" ry="7" fill="#7D4F20" transform="rotate(25 ${cx + 68} ${cy})" />
            <ellipse cx="${cx + 74}" cy="${cy + 15}" rx="11" ry="6" fill="#8F5F2B" transform="rotate(25 ${cx + 74} ${cy + 15})" />
        `;
    } else if (archetype === "soap_bar") {
        heroObject = `
            <ellipse cx="${cx}" cy="${cy + 92}" rx="105" ry="20" fill="#000000" opacity="0.32" />
            <!-- Bamboo Soap Dish -->
            <rect x="${cx - 105}" y="${cy + 70}" width="210" height="18" rx="4" fill="#C2A16E" />
            <line x1="${cx - 65}" y1="${cy + 70}" x2="${cx - 65}" y2="${cy + 88}" stroke="#8F6E3B" stroke-width="2.5" />
            <line x1="${cx - 25}" y1="${cy + 70}" x2="${cx - 25}" y2="${cy + 88}" stroke="#8F6E3B" stroke-width="2.5" />
            <line x1="${cx + 15}" y1="${cy + 70}" x2="${cx + 15}" y2="${cy + 88}" stroke="#8F6E3B" stroke-width="2.5" />
            <line x1="${cx + 55}" y1="${cy + 70}" x2="${cx + 55}" y2="${cy + 88}" stroke="#8F6E3B" stroke-width="2.5" />
            <!-- Botanical Cold Pressed Soap Bar -->
            <path d="M${cx - 75} ${cy - 15} L${cx + 65} ${cy - 40} L${cx + 90} ${cy + 30} L${cx - 50} ${cy + 60} Z" fill="url(#soapGrad_${seed})" filter="url(#dropShadow)" />
            <path d="M${cx - 75} ${cy - 15} L${cx - 50} ${cy + 60} L${cx - 54} ${cy + 68} L${cx - 79} ${cy - 7} Z" fill="#3D533A" />
            <!-- Natural Botanical Flecks -->
            <circle cx="${cx - 15}" cy="${cy}" r="3" fill="#1C2E1A" opacity="0.7" />
            <circle cx="${cx + 15}" cy="${cy + 12}" r="2.5" fill="#1C2E1A" opacity="0.6" />
            <circle cx="${cx + 40}" cy="${cy - 8}" r="3.2" fill="#1C2E1A" opacity="0.8" />
            <circle cx="${cx - 35}" cy="${cy + 22}" r="2" fill="#1C2E1A" opacity="0.5" />
        `;
    } else if (archetype === "tablet_bowl") {
        heroObject = `
            <ellipse cx="${cx}" cy="${cy + 92}" rx="100" ry="20" fill="#000000" opacity="0.32" />
            <!-- Soapstone Dish -->
            <ellipse cx="${cx}" cy="${cy + 72}" rx="96" ry="26" fill="#424D47" filter="url(#dropShadow)" />
            <ellipse cx="${cx}" cy="${cy + 69}" rx="90" ry="22" fill="#58665E" />
            <ellipse cx="${cx}" cy="${cy + 67}" rx="84" ry="18" fill="#323C37" />
            <!-- Classical Round Vatis (Herbal Tablets) -->
            <ellipse cx="${cx - 20}" cy="${cy + 62}" rx="17" ry="13" fill="#3A2A1A" stroke="#22180E" stroke-width="1" />
            <ellipse cx="${cx + 18}" cy="${cy + 65}" rx="16" ry="12" fill="#4B3824" stroke="#291D11" stroke-width="1" />
            <ellipse cx="${cx}" cy="${cy + 58}" rx="18" ry="13" fill="#2E2114" stroke="#1D140B" stroke-width="1" />
            <ellipse cx="${cx - 38}" cy="${cy + 67}" rx="15" ry="11" fill="#443220" stroke="#241A0F" stroke-width="1" />
            <ellipse cx="${cx + 36}" cy="${cy + 61}" rx="15" ry="11" fill="#362617" stroke="#20150B" stroke-width="1" />
            <circle cx="${cx - 3}" cy="${cy + 55}" r="2.5" fill="#9C8165" opacity="0.6" />
        `;
    } else {
        // elixir_bottle
        heroObject = `
            <ellipse cx="${cx}" cy="${cy + 90}" rx="82" ry="18" fill="#000000" opacity="0.36" />
            <path d="M${cx - 65} ${cy - 75} L${cx + 65} ${cy - 75} Q${cx + 74} ${cy - 75} ${cx + 74} ${cy - 60} L${cx + 74} ${cy + 75} Q${cx + 74} ${cy + 92} ${cx + 55} ${cy + 94} L${cx - 55} ${cy + 94} Q${cx - 74} ${cy + 92} ${cx - 74} ${cy + 75} L${cx - 74} ${cy - 60} Q${cx - 74} ${cy - 75} ${cx - 65} ${cy - 75} Z" fill="url(#elixirGrad_${seed})" filter="url(#dropShadow)" />
            <rect x="${cx - 65}" y="${cy - 55}" width="130" height="142" rx="8" fill="#381907" opacity="0.88" />
            <rect x="${cx - 62}" y="${cy - 60}" width="10" height="148" rx="5" fill="#FFFFFF" opacity="0.28" />
            <path d="M${cx - 32} ${cy - 110} L${cx + 32} ${cy - 110} L${cx + 65} ${cy - 75} L${cx - 65} ${cy - 75} Z" fill="#220D03" />
            <rect x="${cx - 20}" y="${cy - 145}" width="40" height="35" rx="2" fill="#351605" />
            <ellipse cx="${cx}" cy="${cy - 145}" rx="22" ry="7" fill="#B3864F" stroke="#7A562B" stroke-width="1.5" />
        `;
    }

    // Natural Botanical Props (Varied placement & elements)
    const propChoice = seed % 6;
    let propSvg = "";
    if (propChoice === 0) {
        // Tulsi Sprig (Right side)
        propSvg = `
            <path d="M${cx + 75} ${cy + 95} Q${cx + 120} ${cy + 60} ${cx + 135} ${cy + 20}" stroke="#3D5A38" stroke-width="3" fill="none" />
            <ellipse cx="${cx + 115}" cy="${cy + 60}" rx="16" ry="9" fill="#4B7744" transform="rotate(-35 ${cx + 115} ${cy + 60})" />
            <ellipse cx="${cx + 130}" cy="${cy + 30}" rx="14" ry="8" fill="#588B50" transform="rotate(25 ${cx + 130} ${cy + 30})" />
        `;
    } else if (propChoice === 1) {
        // Star Anise & Cardamom (Left side)
        propSvg = `
            <g transform="translate(${cx - 110}, ${cy + 80}) scale(0.65)">
                <ellipse cx="0" cy="0" rx="24" ry="8" fill="#5B311B" transform="rotate(0)" />
                <ellipse cx="0" cy="0" rx="24" ry="8" fill="#6D3D24" transform="rotate(45)" />
                <ellipse cx="0" cy="0" rx="24" ry="8" fill="#5B311B" transform="rotate(90)" />
                <ellipse cx="0" cy="0" rx="24" ry="8" fill="#6D3D24" transform="rotate(135)" />
                <circle cx="0" cy="0" r="7" fill="#3E1E0F" />
            </g>
            <ellipse cx="${cx - 140}" cy="${cy + 92}" rx="11" ry="6" fill="#7D9962" stroke="#5A7342" stroke-width="1" transform="rotate(-20 ${cx - 140} ${cy + 92})" />
        `;
    } else if (propChoice === 2) {
        // Dried Rose Petals
        propSvg = `
            <path d="M${cx + 110} ${cy + 85} C${cx + 100} ${cy + 70} ${cx + 125} ${cy + 65} ${cx + 130} ${cy + 77} C${cx + 135} ${cy + 90} ${cx + 118} ${cy + 93} ${cx + 110} ${cy + 85} Z" fill="#992D3D" opacity="0.9" />
            <path d="M${cx + 85} ${cy + 95} C${cx + 78} ${cy + 85} ${cx + 95} ${cy + 80} ${cx + 100} ${cy + 89} C${cx + 105} ${cy + 97} ${cx + 91} ${cy + 100} ${cx + 85} ${cy + 95} Z" fill="#B83A4C" opacity="0.85" />
        `;
    } else if (propChoice === 3) {
        // Raw Turmeric Segment
        propSvg = `
            <path d="M${cx - 135} ${cy + 85} Q${cx - 105} ${cy + 75} ${cx - 85} ${cy + 90} Q${cx - 110} ${cy + 100} ${cx - 135} ${cy + 85} Z" fill="#D9822B" stroke="#8C4B0E" stroke-width="1.5" />
            <ellipse cx="${cx - 85}" cy="${cy + 90}" rx="6" ry="9" fill="#F5A623" />
        `;
    } else if (propChoice === 4) {
        // Fresh Green Amla Fruit
        propSvg = `
            <circle cx="${cx + 115}" cy="${cy + 85}" r="16" fill="#7FA94E" filter="url(#dropShadow)" />
            <path d="M${cx + 115} ${cy + 69} C${cx + 108} ${cy + 76} ${cx + 108} ${cy + 94} ${cx + 115} ${cy + 101}" stroke="#688F3D" stroke-width="1" fill="none" />
            <circle cx="${cx + 115}" cy="${cy + 69}" r="2.2" fill="#4B662C" />
        `;
    } else {
        // Cinnamon Quill
        propSvg = `
            <path d="M${cx - 140} ${cy + 80} L${cx - 85} ${cy + 95} L${cx - 90} ${cy + 102} L${cx - 145} ${cy + 87} Z" fill="#6E3D1D" />
            <ellipse cx="${cx - 142}" cy="${cy + 83}" rx="4" ry="6" fill="#4E2711" stroke="#2E1407" stroke-width="1" />
        `;
    }

    // Diagonal Background Foliage Shadow / Silhouette for depth
    const foliageAngle = (seed * 31) % 360;
    const foliageSvg = `
        <!-- Soft Foliage Ambient Shadow -->
        <g opacity="0.08" transform="rotate(${foliageAngle} 300 300)">
            <path d="M0 0 Q150 100 300 150 Q450 100 600 0 L600 100 Q300 350 0 100 Z" fill="#14281B" filter="url(#dropShadow)" />
        </g>
    `;

    return `
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 600 600" width="600" height="600">
    <defs>
        <!-- Directional Studio Background -->
        <linearGradient id="studioBg_${seed}" x1="${lx1}%" y1="${ly1}%" x2="${lx2}%" y2="${ly2}%">
            <stop offset="0%" stop-color="hsl(${palette.h1}, ${palette.s1}%, ${palette.l1}%)" />
            <stop offset="60%" stop-color="hsl(${palette.h2}, ${palette.s2}%, ${palette.l2}%)" />
            <stop offset="100%" stop-color="hsl(${palette.h2}, ${palette.s2 + 8}%, ${palette.l2 - 10}%)" />
        </linearGradient>

        <radialGradient id="centerGlow_${seed}" cx="50%" cy="40%" r="55%">
            <stop offset="0%" stop-color="#FFFFFF" stop-opacity="0.45" />
            <stop offset="100%" stop-color="#FFFFFF" stop-opacity="0" />
        </radialGradient>

        <filter id="dropShadow" x="-30%" y="-30%" width="160%" height="160%">
            <feDropShadow dx="0" dy="16" stdDeviation="15" flood-color="rgba(20, 32, 24, 0.25)" />
        </filter>

        <!-- Materials -->
        <linearGradient id="jarGrad_${seed}" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stop-color="#3D1C08" />
            <stop offset="30%" stop-color="#7A3D14" />
            <stop offset="70%" stop-color="#4E230A" />
            <stop offset="100%" stop-color="#240D03" />
        </linearGradient>

        <linearGradient id="brassCap_${seed}" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stop-color="#8C6721" />
            <stop offset="40%" stop-color="#E5B856" />
            <stop offset="75%" stop-color="#F2D179" />
            <stop offset="100%" stop-color="#7A5617" />
        </linearGradient>

        <linearGradient id="dropperGrad_${seed}" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stop-color="#14281A" />
            <stop offset="35%" stop-color="#2D5436" />
            <stop offset="75%" stop-color="#1A3320" />
            <stop offset="100%" stop-color="#0E1C12" />
        </linearGradient>

        <linearGradient id="potGrad_${seed}" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stop-color="#6E3C1D" />
            <stop offset="40%" stop-color="#A15B30" />
            <stop offset="75%" stop-color="#824622" />
            <stop offset="100%" stop-color="#4F260E" />
        </linearGradient>

        <linearGradient id="powderGrad_${seed}" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stop-color="#E5BF77" />
            <stop offset="60%" stop-color="#C49B50" />
            <stop offset="100%" stop-color="#8C6B2B" />
        </linearGradient>

        <linearGradient id="cupGrad_${seed}" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stop-color="#CCC2B4" />
            <stop offset="45%" stop-color="#EFE9DF" />
            <stop offset="80%" stop-color="#DFD7CA" />
            <stop offset="100%" stop-color="#B8ACA0" />
        </linearGradient>

        <linearGradient id="brewGrad_${seed}" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stop-color="#F5A623" />
            <stop offset="100%" stop-color="#9C5B10" />
        </linearGradient>

        <linearGradient id="martabanGrad_${seed}" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stop-color="#D9D0C1" />
            <stop offset="40%" stop-color="#FAF5EC" />
            <stop offset="80%" stop-color="#E5DDD0" />
            <stop offset="100%" stop-color="#BAAF9E" />
        </linearGradient>

        <linearGradient id="honeyJarGrad_${seed}" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stop-color="#5E3809" />
            <stop offset="35%" stop-color="#B87B1D" />
            <stop offset="70%" stop-color="#8F590F" />
            <stop offset="100%" stop-color="#422504" />
        </linearGradient>

        <linearGradient id="pureHoneyGrad_${seed}" x1="0%" y1="0%" x2="0%" y2="100%">
            <stop offset="0%" stop-color="#FDB827" />
            <stop offset="60%" stop-color="#E69500" />
            <stop offset="100%" stop-color="#B86E00" />
        </linearGradient>

        <linearGradient id="soapGrad_${seed}" x1="0%" y1="0%" x2="100%" y2="100%">
            <stop offset="0%" stop-color="#6F886B" />
            <stop offset="50%" stop-color="#556F51" />
            <stop offset="100%" stop-color="#3C5139" />
        </linearGradient>

        <linearGradient id="elixirGrad_${seed}" x1="0%" y1="0%" x2="100%" y2="0%">
            <stop offset="0%" stop-color="#2D1204" />
            <stop offset="35%" stop-color="#54280E" />
            <stop offset="70%" stop-color="#3D1B08" />
            <stop offset="100%" stop-color="#1A0A02" />
        </linearGradient>
    </defs>

    <!-- Studio Backdrop -->
    <rect width="600" height="600" fill="url(#studioBg_${seed})" />
    <rect width="600" height="600" fill="url(#centerGlow_${seed})" />

    <!-- Ambient Shadow Trace -->
    ${foliageSvg}

    <!-- Architectural Display Pedestal -->
    ${podiumSvg}

    <!-- Hero Object with subtle tilt/scale -->
    <g transform="rotate(${rotObj} ${cx} ${cy}) scale(${objScale} ${objScale})">
        ${heroObject}
    </g>

    <!-- Complementary Botanical Accent -->
    ${propSvg}
</svg>`;
}

// -----------------------------------------------------------------------------
// DHASH COMPUTATION (64-bit difference hash)
// -----------------------------------------------------------------------------
async function computeDHashFromBuffer(buf) {
    const raw = await sharp(buf)
        .resize(9, 8, { fit: 'fill' })
        .greyscale()
        .raw()
        .toBuffer();

    let h = 0n;
    for (let r = 0; r < 8; r++) {
        for (let c = 0; c < 8; c++) {
            const left = raw[r * 9 + c];
            const right = raw[r * 9 + c + 1];
            h = (h << 1n) | (left > right ? 1n : 0n);
        }
    }
    return h;
}

function hammingDistance(h1, h2) {
    let x = h1 ^ h2;
    let dist = 0;
    while (x > 0n) {
        if ((x & 1n) === 1n) dist++;
        x >>= 1n;
    }
    return dist;
}

// -----------------------------------------------------------------------------
// MAIN BATCH RUNNER & REGENERATION LOOP
// -----------------------------------------------------------------------------
async function run() {
    console.log("=================================================================");
    console.log("🌿 GENERATING ALL 312 DISTINCT PRODUCT IMAGES (600x600 WebP)");
    console.log("=================================================================\n");

    const BATCH_SIZE = 40;
    const total = products.length;
    const hashes = [];

    // Step 1: Render in batches of 40
    for (let i = 0; i < total; i += BATCH_SIZE) {
        const batchNum = Math.floor(i / BATCH_SIZE) + 1;
        const currentBatch = products.slice(i, i + BATCH_SIZE);
        console.log(`Processing Batch ${batchNum}/${Math.ceil(total / BATCH_SIZE)} (Products ${i + 1} to ${Math.min(i + BATCH_SIZE, total)})...`);

        const results = await Promise.all(currentBatch.map(async (p, bIdx) => {
            const idx = i + bIdx;
            const filename = path.basename(p.imageUrl);
            const targetPath = path.join(OUTPUT_DIR, filename);

            let variantSeed = 0;
            let webpBuf;
            let quality = 72;

            const svg = buildSvgScene(p, idx, variantSeed);
            webpBuf = await sharp(Buffer.from(svg))
                .resize(600, 600, { fit: 'cover' })
                .webp({ quality, effort: 6 })
                .toBuffer();

            // Enforce max 60 KB
            while (webpBuf.length > 60 * 1024 && quality > 30) {
                quality -= 5;
                webpBuf = await sharp(Buffer.from(svg))
                    .resize(600, 600, { fit: 'cover' })
                    .webp({ quality, effort: 6 })
                    .toBuffer();
            }

            fs.writeFileSync(targetPath, webpBuf);
            const hash = await computeDHashFromBuffer(webpBuf);
            return { index: idx, filename, targetPath, hash, size: webpBuf.length, product: p, variantSeed, quality };
        }));

        hashes.push(...results);
        console.log(`  ✓ Batch ${batchNum} done. Total finished: ${hashes.length}/${total}`);
    }

    // Step 2: Check & Regenerate any pair with dHash distance < 6
    console.log("\n--- Checking dHash Distance (< 6) & Multi-Pass Resolving Conflicts ---");
    let regeneratedCount = 0;

    for (let pass = 1; pass <= 5; pass++) {
        let conflictsInPass = 0;
        for (let i = 0; i < hashes.length; i++) {
            for (let j = i + 1; j < hashes.length; j++) {
                let dist = hammingDistance(hashes[i].hash, hashes[j].hash);
                if (dist < 6) {
                    conflictsInPass++;
                    regeneratedCount++;
                    hashes[j].variantSeed = (hashes[j].variantSeed || 0) + 13 + pass * 7;

                    const newSvg = buildSvgScene(hashes[j].product, hashes[j].index, hashes[j].variantSeed);
                    let quality = 72;
                    let newBuf = await sharp(Buffer.from(newSvg))
                        .resize(600, 600, { fit: 'cover' })
                        .webp({ quality, effort: 6 })
                        .toBuffer();

                    while (newBuf.length > 60 * 1024 && quality > 30) {
                        quality -= 5;
                        newBuf = await sharp(Buffer.from(newSvg))
                            .resize(600, 600, { fit: 'cover' })
                            .webp({ quality, effort: 6 })
                            .toBuffer();
                    }

                    fs.writeFileSync(hashes[j].targetPath, newBuf);
                    hashes[j].hash = await computeDHashFromBuffer(newBuf);
                    hashes[j].size = newBuf.length;
                }
            }
        }
        if (conflictsInPass === 0) {
            console.log(`  ✓ Multi-pass dHash resolved all conflicts cleanly in pass ${pass}.`);
            break;
        }
    }

    // Step 3: Final Verification & Totals
    console.log("\n=================================================================");
    console.log("📊 FINAL PRODUCT IMAGE TOTALS & DHASH AUDIT");
    console.log("=================================================================\n");

    let minSize = Infinity;
    let maxSize = 0;
    let totalBytes = 0;
    let over60kCount = 0;

    for (const h of hashes) {
        totalBytes += h.size;
        if (h.size < minSize) minSize = h.size;
        if (h.size > maxSize) maxSize = h.size;
        if (h.size > 60 * 1024) over60kCount++;
    }

    let finalLowPairs = 0;
    for (let i = 0; i < hashes.length; i++) {
        for (let j = i + 1; j < hashes.length; j++) {
            if (hammingDistance(hashes[i].hash, hashes[j].hash) < 6) {
                finalLowPairs++;
            }
        }
    }

    console.log(`Total Products: ${products.length}`);
    console.log(`Min File Size: ${(minSize / 1024).toFixed(2)} KB`);
    console.log(`Max File Size: ${(maxSize / 1024).toFixed(2)} KB`);
    console.log(`Average File Size: ${(totalBytes / products.length / 1024).toFixed(2)} KB`);
    console.log(`Total Directory Size: ${(totalBytes / (1024 * 1024)).toFixed(2)} MB (Hard Budget: <15 MB)`);
    console.log(`Files over 60 KB: ${over60kCount} (Target: 0)`);
    console.log(`Pairs with dHash < 6: ${finalLowPairs} (Target: 0)`);
    console.log(`Regenerated to resolve distance: ${regeneratedCount}`);
    console.log("=================================================================\n");
}

run().catch(err => {
    console.error("Execution failed:", err);
    process.exit(1);
});
