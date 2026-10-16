/**
 * The Great Ayurveda - Brand Asset & Icon Generator
 * Generates:
 *  - logo-full.svg, logo-mark.svg, logo-dark.svg, logo.svg
 *  - admin-logo-full.svg, admin-logo-dark.svg, admin-logo-mark.svg
 *  - favicon.ico (16, 32, 48), favicon-32.png, favicon-32x32.png
 *  - apple-touch-icon.png (180x180)
 *  - android-chrome-192x192.png, android-chrome-512x512.png, maskable icon
 *  - og-image.png (1200x630)
 *  - site.webmanifest & admin.webmanifest
 */

const fs = require('fs');
const path = require('path');
const zlib = require('zlib');

// Palettes
const BRAND = {
  forestGreen: '#1F5E3B',
  turmericGold: '#E3A72F',
  cream: '#FBF6EA',
  clay: '#A4553A',
  ink: '#1B2A22',
  forestDark: '#123823',
  goldLight: '#F7C85C',
  clayLight: '#C46D50',
};

// -------------------------------------------------------------
// SVG DEFINITIONS
// -------------------------------------------------------------

function getLogoMarkSvg() {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100" height="100">
  <defs>
    <linearGradient id="forestGrad" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#2D7A4F" />
      <stop offset="100%" stop-color="#14442A" />
    </linearGradient>
    <linearGradient id="goldGrad" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#F7C85C" />
      <stop offset="100%" stop-color="#C68E22" />
    </linearGradient>
    <linearGradient id="clayGrad" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#C0674B" />
      <stop offset="100%" stop-color="#8E3C23" />
    </linearGradient>
    <radialGradient id="auraGlow" cx="50%" cy="50%" r="50%">
      <stop offset="0%" stop-color="#E3A72F" stop-opacity="0.25" />
      <stop offset="100%" stop-color="#E3A72F" stop-opacity="0" />
    </radialGradient>
  </defs>

  <!-- Sacred Aura Ring -->
  <circle cx="50" cy="50" r="47" fill="url(#auraGlow)" />
  <circle cx="50" cy="50" r="46" fill="none" stroke="#E3A72F" stroke-width="1.2" stroke-dasharray="3,2.5" opacity="0.65" />
  <circle cx="50" cy="50" r="42" fill="none" stroke="#1F5E3B" stroke-width="0.8" opacity="0.4" />

  <!-- Calyx Base (Ayurvedic Healing Bowl / Earth Element) -->
  <path d="M 32,77 C 37,84 63,84 68,77 C 62,81 38,81 32,77 Z" fill="url(#clayGrad)" />
  <ellipse cx="50" cy="79.5" rx="7" ry="2" fill="url(#goldGrad)" opacity="0.8" />

  <!-- Outer Lotus Petals (Forest Green) -->
  <path d="M 50,76 C 22,69 12,47 23,30 C 33,45 42,62 50,76 Z" fill="url(#forestGrad)" />
  <path d="M 50,76 C 78,69 88,47 77,30 C 67,45 58,62 50,76 Z" fill="url(#forestGrad)" />

  <!-- Inner Radiant Petals (Turmeric Gold) -->
  <path d="M 50,76 C 31,64 24,46 35,34 C 43,47 47,63 50,76 Z" fill="url(#goldGrad)" opacity="0.95" />
  <path d="M 50,76 C 69,64 76,46 65,34 C 57,47 53,63 50,76 Z" fill="url(#goldGrad)" opacity="0.95" />

  <!-- Central Healing Leaf (Tulsi / Medicinal Flora) -->
  <path d="M 50,77 C 39,58 37,39 50,18 C 63,39 61,58 50,77 Z" fill="url(#forestGrad)" />

  <!-- Sacred Leaf Veins (Cream) -->
  <path d="M 50,75 C 50,56 50,37 50,20" stroke="#FBF6EA" stroke-width="1.2" stroke-linecap="round" fill="none" opacity="0.9" />
  <path d="M 50,61 Q 45,55 43,52 M 50,50 Q 45,44 44,41 M 50,39 Q 46,33 46,31" stroke="#FBF6EA" stroke-width="0.8" stroke-linecap="round" fill="none" opacity="0.75" />
  <path d="M 50,61 Q 55,55 57,52 M 50,50 Q 55,44 56,41 M 50,39 Q 54,33 54,31" stroke="#FBF6EA" stroke-width="0.8" stroke-linecap="round" fill="none" opacity="0.75" />

  <!-- Golden Bindu / Ojas (Vital Essence) -->
  <circle cx="50" cy="12.5" r="3.5" fill="url(#goldGrad)" />
  <circle cx="50" cy="12.5" r="1.5" fill="#FBF6EA" />
</svg>`;
}

function getLogoFullSvg() {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 460 110" width="460" height="110">
  <defs>
    <linearGradient id="forestGradF" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#2D7A4F" />
      <stop offset="100%" stop-color="#14442A" />
    </linearGradient>
    <linearGradient id="goldGradF" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#F7C85C" />
      <stop offset="100%" stop-color="#C68E22" />
    </linearGradient>
    <linearGradient id="clayGradF" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#C0674B" />
      <stop offset="100%" stop-color="#8E3C23" />
    </linearGradient>
  </defs>

  <!-- Left Emblem (scale 0.95, offset 10, 8) -->
  <g transform="translate(10, 8) scale(0.94)">
    <circle cx="50" cy="50" r="46" fill="none" stroke="#E3A72F" stroke-width="1.2" stroke-dasharray="3,2.5" opacity="0.65" />
    <circle cx="50" cy="50" r="42" fill="none" stroke="#1F5E3B" stroke-width="0.8" opacity="0.4" />
    <path d="M 32,77 C 37,84 63,84 68,77 C 62,81 38,81 32,77 Z" fill="url(#clayGradF)" />
    <ellipse cx="50" cy="79.5" rx="7" ry="2" fill="url(#goldGradF)" opacity="0.8" />
    <path d="M 50,76 C 22,69 12,47 23,30 C 33,45 42,62 50,76 Z" fill="url(#forestGradF)" />
    <path d="M 50,76 C 78,69 88,47 77,30 C 67,45 58,62 50,76 Z" fill="url(#forestGradF)" />
    <path d="M 50,76 C 31,64 24,46 35,34 C 43,47 47,63 50,76 Z" fill="url(#goldGradF)" opacity="0.95" />
    <path d="M 50,76 C 69,64 76,46 65,34 C 57,47 53,63 50,76 Z" fill="url(#goldGradF)" opacity="0.95" />
    <path d="M 50,77 C 39,58 37,39 50,18 C 63,39 61,58 50,77 Z" fill="url(#forestGradF)" />
    <path d="M 50,75 C 50,56 50,37 50,20" stroke="#FBF6EA" stroke-width="1.2" stroke-linecap="round" fill="none" opacity="0.9" />
    <circle cx="50" cy="12.5" r="3.5" fill="url(#goldGradF)" />
    <circle cx="50" cy="12.5" r="1.5" fill="#FBF6EA" />
  </g>

  <!-- Divider Line -->
  <line x1="120" y1="20" x2="120" y2="90" stroke="#E3A72F" stroke-width="1" opacity="0.35" />

  <!-- Wordmark Typography -->
  <text x="138" y="44" font-family="'Cinzel', 'Playfair Display', Georgia, serif" font-size="24" font-weight="700" fill="#1F5E3B" letter-spacing="3.5">THE GREAT</text>
  <text x="138" y="74" font-family="'Cinzel', 'Playfair Display', Georgia, serif" font-size="28" font-weight="800" fill="#E3A72F" letter-spacing="5">AYURVEDA</text>
  
  <!-- Subtle Tagline -->
  <text x="140" y="94" font-family="'Plus Jakarta Sans', system-ui, sans-serif" font-size="9" font-weight="600" fill="#A4553A" letter-spacing="2.2" opacity="0.9">AUTHENTIC CARE &amp; CONSULTATION</text>
</svg>`;
}

function getLogoDarkSvg() {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 460 110" width="460" height="110">
  <defs>
    <linearGradient id="forestGradD" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#38A169" />
      <stop offset="100%" stop-color="#1F5E3B" />
    </linearGradient>
    <linearGradient id="goldGradD" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#FFD166" />
      <stop offset="100%" stop-color="#E3A72F" />
    </linearGradient>
  </defs>

  <!-- Left Emblem -->
  <g transform="translate(10, 8) scale(0.94)">
    <circle cx="50" cy="50" r="46" fill="none" stroke="#FFD166" stroke-width="1.2" stroke-dasharray="3,2.5" opacity="0.75" />
    <circle cx="50" cy="50" r="42" fill="none" stroke="#38A169" stroke-width="0.8" opacity="0.5" />
    <path d="M 32,77 C 37,84 63,84 68,77 C 62,81 38,81 32,77 Z" fill="#C0674B" />
    <path d="M 50,76 C 22,69 12,47 23,30 C 33,45 42,62 50,76 Z" fill="url(#forestGradD)" />
    <path d="M 50,76 C 78,69 88,47 77,30 C 67,45 58,62 50,76 Z" fill="url(#forestGradD)" />
    <path d="M 50,76 C 31,64 24,46 35,34 C 43,47 47,63 50,76 Z" fill="url(#goldGradD)" opacity="0.95" />
    <path d="M 50,76 C 69,64 76,46 65,34 C 57,47 53,63 50,76 Z" fill="url(#goldGradD)" opacity="0.95" />
    <path d="M 50,77 C 39,58 37,39 50,18 C 63,39 61,58 50,77 Z" fill="url(#forestGradD)" />
    <path d="M 50,75 C 50,56 50,37 50,20" stroke="#FBF6EA" stroke-width="1.2" stroke-linecap="round" fill="none" opacity="0.95" />
    <circle cx="50" cy="12.5" r="3.5" fill="url(#goldGradD)" />
    <circle cx="50" cy="12.5" r="1.5" fill="#FFFFFF" />
  </g>

  <!-- Divider Line -->
  <line x1="120" y1="20" x2="120" y2="90" stroke="#FFD166" stroke-width="1" opacity="0.4" />

  <!-- Wordmark Typography for Dark Backgrounds -->
  <text x="138" y="44" font-family="'Cinzel', 'Playfair Display', Georgia, serif" font-size="24" font-weight="700" fill="#FBF6EA" letter-spacing="3.5">THE GREAT</text>
  <text x="138" y="74" font-family="'Cinzel', 'Playfair Display', Georgia, serif" font-size="28" font-weight="800" fill="#FFD166" letter-spacing="5">AYURVEDA</text>
  
  <text x="140" y="94" font-family="'Plus Jakarta Sans', system-ui, sans-serif" font-size="9" font-weight="600" fill="#E3A72F" letter-spacing="2.2" opacity="0.9">AUTHENTIC CARE &amp; CONSULTATION</text>
</svg>`;
}

function getAdminLogoFullSvg() {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 520 110" width="520" height="110">
  <defs>
    <linearGradient id="forestGradA" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#38A169" />
      <stop offset="100%" stop-color="#1F5E3B" />
    </linearGradient>
    <linearGradient id="goldGradA" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#FFD166" />
      <stop offset="100%" stop-color="#E3A72F" />
    </linearGradient>
    <linearGradient id="clayBadge" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#C0674B" />
      <stop offset="100%" stop-color="#8E3C23" />
    </linearGradient>
  </defs>

  <!-- Left Emblem -->
  <g transform="translate(10, 8) scale(0.94)">
    <circle cx="50" cy="50" r="46" fill="none" stroke="#FFD166" stroke-width="1.2" stroke-dasharray="3,2.5" opacity="0.75" />
    <path d="M 32,77 C 37,84 63,84 68,77 C 62,81 38,81 32,77 Z" fill="#C0674B" />
    <path d="M 50,76 C 22,69 12,47 23,30 C 33,45 42,62 50,76 Z" fill="url(#forestGradA)" />
    <path d="M 50,76 C 78,69 88,47 77,30 C 67,45 58,62 50,76 Z" fill="url(#forestGradA)" />
    <path d="M 50,76 C 31,64 24,46 35,34 C 43,47 47,63 50,76 Z" fill="url(#goldGradA)" />
    <path d="M 50,76 C 69,64 76,46 65,34 C 57,47 53,63 50,76 Z" fill="url(#goldGradA)" />
    <path d="M 50,77 C 39,58 37,39 50,18 C 63,39 61,58 50,77 Z" fill="url(#forestGradA)" />
    <path d="M 50,75 C 50,56 50,37 50,20" stroke="#FBF6EA" stroke-width="1.2" stroke-linecap="round" fill="none" opacity="0.95" />
    <circle cx="50" cy="12.5" r="3.5" fill="url(#goldGradA)" />
  </g>

  <!-- Divider Line -->
  <line x1="120" y1="20" x2="120" y2="90" stroke="#FFD166" stroke-width="1" opacity="0.4" />

  <!-- Typography -->
  <text x="138" y="44" font-family="'Cinzel', 'Playfair Display', Georgia, serif" font-size="24" font-weight="700" fill="#FBF6EA" letter-spacing="3.5">THE GREAT</text>
  <text x="138" y="74" font-family="'Cinzel', 'Playfair Display', Georgia, serif" font-size="28" font-weight="800" fill="#FFD166" letter-spacing="5">AYURVEDA</text>
  <!-- Prominent Admin Badge -->
  <g transform="translate(138, 83)">
    <rect x="0" y="0" width="112" height="22" rx="11" fill="url(#clayBadge)" stroke="#FFD166" stroke-width="1" />
    <text x="56" y="15" font-family="system-ui, -apple-system, sans-serif" font-size="10" font-weight="800" fill="#FFFFFF" text-anchor="middle" letter-spacing="1.5">ADMIN PORTAL</text>
  </g>
</svg>`;
}

function getAdminLogoDarkSvg() {
  return getAdminLogoFullSvg();
}

function getAdminLogoMarkSvg() {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100" height="100">
  <defs>
    <linearGradient id="forestGradAM" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#2D7A4F" />
      <stop offset="100%" stop-color="#14442A" />
    </linearGradient>
    <linearGradient id="goldGradAM" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#F7C85C" />
      <stop offset="100%" stop-color="#C68E22" />
    </linearGradient>
    <linearGradient id="clayBadgeM" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="#C0674B" />
      <stop offset="100%" stop-color="#8E3C23" />
    </linearGradient>
  </defs>

  <!-- Aura Ring -->
  <circle cx="50" cy="50" r="47" fill="#18221D" />
  <circle cx="50" cy="50" r="46" fill="none" stroke="#E3A72F" stroke-width="1.8" stroke-dasharray="3,2" />

  <!-- Lotus & Leaf -->
  <path d="M 50,74 C 22,67 12,47 23,32 C 33,45 42,60 50,74 Z" fill="url(#forestGradAM)" />
  <path d="M 50,74 C 78,67 88,47 77,32 C 67,45 58,60 50,74 Z" fill="url(#forestGradAM)" />
  <path d="M 50,74 C 31,63 24,46 35,35 C 43,47 47,61 50,74 Z" fill="url(#goldGradAM)" opacity="0.9" />
  <path d="M 50,74 C 69,63 76,46 65,35 C 57,47 53,61 50,74 Z" fill="url(#goldGradAM)" opacity="0.9" />
  <path d="M 50,74 C 39,57 37,39 50,18 C 63,39 61,57 50,74 Z" fill="url(#forestGradAM)" />
  <circle cx="50" cy="13" r="3" fill="url(#goldGradAM)" />

  <!-- Admin Shield Badge at bottom -->
  <g transform="translate(32, 68)">
    <rect x="0" y="0" width="36" height="18" rx="6" fill="url(#clayBadgeM)" stroke="#E3A72F" stroke-width="1.2" />
    <text x="18" y="13" font-family="system-ui, sans-serif" font-size="9" font-weight="900" fill="#FFFFFF" text-anchor="middle">ADM</text>
  </g>
</svg>`;
}

// -------------------------------------------------------------
// PURE NODE.JS PNG & ICO ENCODER
// -------------------------------------------------------------

function crc32(buf) {
  let table = new Uint32Array(256);
  for (let i = 0; i < 256; i++) {
    let c = i;
    for (let k = 0; k < 8; k++) c = ((c & 1) ? (0xEDB88320 ^ (c >>> 1)) : (c >>> 1));
    table[i] = c;
  }
  let crc = -1;
  for (let i = 0; i < buf.length; i++) crc = (crc >>> 8) ^ table[(crc ^ buf[i]) & 0xFF];
  return (crc ^ (-1)) >>> 0;
}

function makeChunk(type, data) {
  const len = data.length;
  const buf = Buffer.alloc(12 + len);
  buf.writeUInt32BE(len, 0);
  buf.write(type, 4, 4, 'ascii');
  data.copy(buf, 8);
  const typeAndData = buf.subarray(4, 8 + len);
  buf.writeUInt32BE(crc32(typeAndData), 8 + len);
  return buf;
}

function createPng(w, h, rgbaCallback) {
  const sig = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(w, 0);
  ihdr.writeUInt32BE(h, 4);
  ihdr[8] = 8; ihdr[9] = 6; ihdr[10] = 0; ihdr[11] = 0; ihdr[12] = 0;
  
  const raw = Buffer.alloc(h * (1 + w * 4));
  for (let y = 0; y < h; y++) {
    const rowOffset = y * (1 + w * 4);
    raw[rowOffset] = 0; // Filter: None
    for (let x = 0; x < w; x++) {
      const [r, g, b, a] = rgbaCallback(x, y, w, h);
      const pxOffset = rowOffset + 1 + x * 4;
      raw[pxOffset] = r;
      raw[pxOffset + 1] = g;
      raw[pxOffset + 2] = b;
      raw[pxOffset + 3] = a;
    }
  }
  const idat = zlib.deflateSync(raw, { level: 9 });
  const iend = Buffer.alloc(0);
  return Buffer.concat([sig, makeChunk('IHDR', ihdr), makeChunk('IDAT', idat), makeChunk('IEND', iend)]);
}

function buildIco(pngBuffers, sizes) {
  const count = pngBuffers.length;
  const header = Buffer.alloc(6);
  header.writeUInt16LE(0, 0); // reserved
  header.writeUInt16LE(1, 2); // type 1 = icon
  header.writeUInt16LE(count, 4);
  const dirEntries = [];
  let offset = 6 + (count * 16);
  for (let i = 0; i < count; i++) {
    const entry = Buffer.alloc(16);
    const size = sizes[i];
    entry[0] = size >= 256 ? 0 : size;
    entry[1] = size >= 256 ? 0 : size;
    entry[2] = 0;
    entry[3] = 0;
    entry.writeUInt16LE(1, 4); // color planes
    entry.writeUInt16LE(32, 6); // bpp
    entry.writeUInt32LE(pngBuffers[i].length, 8);
    entry.writeUInt32LE(offset, 12);
    dirEntries.push(entry);
    offset += pngBuffers[i].length;
  }
  return Buffer.concat([header, ...dirEntries, ...pngBuffers]);
}

// -------------------------------------------------------------
// BRAND ICON RENDERER (Clean Antialiased Math)
// -------------------------------------------------------------

function renderBrandIconPixel(x, y, w, h, options = {}) {
  const isAdmin = options.isAdmin || false;
  const isMaskable = options.isMaskable || false;

  // Normalized coords in [-1, 1]
  const scale = isMaskable ? 0.72 : 0.88;
  const cx = w / 2;
  const cy = h / 2;
  const nx = ((x - cx) / (cx)) / scale;
  const ny = ((y - cy) / (cy)) / scale;

  const r = Math.sqrt(nx * nx + ny * ny);

  // Maskable background fill
  if (isMaskable && r > 1.0) {
    return [31, 94, 59, 255]; // Deep Forest Green
  }

  // Outer aura ring
  if (r >= 0.90 && r <= 0.98) {
    const ringAlpha = Math.min(255, Math.floor(255 * (1 - Math.abs(r - 0.94) / 0.04)));
    if (isAdmin) {
      return [227, 167, 47, ringAlpha]; // Gold ring for admin
    }
    return [227, 167, 47, Math.floor(ringAlpha * 0.85)];
  }

  // Bindu at top
  const binduDx = nx;
  const binduDy = ny - (-0.72);
  const binduDist = Math.sqrt(binduDx * binduDx + binduDy * binduDy);
  if (binduDist < 0.09) {
    if (binduDist < 0.04) return [251, 246, 234, 255]; // Cream center
    return [227, 167, 47, 255]; // Gold bindu
  }

  // Central medicinal leaf
  // ny ranges from -0.6 to +0.6
  if (ny >= -0.60 && ny <= 0.58) {
    const leafProgress = (ny - (-0.60)) / 1.18; // 0 to 1
    const halfWidth = 0.28 * Math.sin(leafProgress * Math.PI);
    if (Math.abs(nx) <= halfWidth) {
      // Central vein
      if (Math.abs(nx) < 0.015) {
        return [251, 246, 234, 220];
      }
      // Leaf body gradient (Deep forest green)
      const grad = Math.floor(31 + leafProgress * 20);
      return [31, Math.min(255, 94 + Math.floor(leafProgress * 15)), 59, 255];
    }
  }

  // Left & Right Inner Gold Petals
  // Centered around ny ~ 0.1, expanding outward
  const petalLeftCenter = { x: -0.28, y: 0.05 };
  const petalRightCenter = { x: 0.28, y: 0.05 };
  const dPl = Math.hypot(nx - petalLeftCenter.x, (ny - petalLeftCenter.y) * 1.3);
  const dPr = Math.hypot(nx - petalRightCenter.x, (ny - petalRightCenter.y) * 1.3);
  if ((dPl < 0.22 && ny <= 0.58) || (dPr < 0.22 && ny <= 0.58)) {
    return [227, 167, 47, 240]; // Turmeric Gold
  }

  // Outer Lotus Petals
  const dOuterL = Math.hypot(nx - (-0.42), (ny - 0.15) * 1.4);
  const dOuterR = Math.hypot(nx - (0.42), (ny - 0.15) * 1.4);
  if ((dOuterL < 0.26 && ny <= 0.62) || (dOuterR < 0.26 && ny <= 0.62)) {
    return [20, 68, 42, 230]; // Rich Forest
  }

  // Base calyx / Ayurvedic bowl
  if (ny >= 0.52 && ny <= 0.68 && Math.abs(nx) <= (0.42 - (ny - 0.52) * 0.4)) {
    if (isAdmin) {
      return [164, 85, 58, 255]; // Terracotta clay badge
    }
    return [164, 85, 58, 240];
  }

  // Admin banner badge indicator for small icons
  if (isAdmin && ny >= 0.45 && ny <= 0.75 && Math.abs(nx) <= 0.55) {
    return [164, 85, 58, 255]; // Clay badge
  }

  // Background
  if (isMaskable) {
    return [31, 94, 59, 255];
  }
  return [0, 0, 0, 0]; // Transparent
}

// -------------------------------------------------------------
// OG IMAGE GENERATOR (1200 x 630 Beautiful Brand Card)
// -------------------------------------------------------------

function renderOgImagePixel(x, y, w, h) {
  // Rich Forest Green gradient backdrop (#123823 -> #1F5E3B -> #0F2A1B)
  const normY = y / h;
  const normX = x / w;
  
  let bgR = Math.floor(18 + normY * 15 - normX * 8);
  let bgG = Math.floor(52 + normY * 42 - normX * 15);
  let bgB = Math.floor(34 + normY * 25 - normX * 10);

  // Outer gold luxury border (8px)
  if (x < 12 || x > w - 12 || y < 12 || y > h - 12) {
    if (x >= 8 && x <= w - 8 && y >= 8 && y <= h - 8) {
      return [227, 167, 47, 255]; // Turmeric Gold
    }
    return [164, 85, 58, 255]; // Clay outer trim
  }

  // Left emblem circular area (center x: 260, y: 315, radius: 190)
  const emblemCx = 260;
  const emblemCy = 315;
  const edx = x - emblemCx;
  const edy = y - emblemCy;
  const edist = Math.hypot(edx, edy);

  if (edist < 180) {
    const iconPixel = renderBrandIconPixel(x - (emblemCx - 180), y - (emblemCy - 180), 360, 360, { isMaskable: false });
    if (iconPixel[3] > 0) {
      const alpha = iconPixel[3] / 255;
      return [
        Math.floor(iconPixel[0] * alpha + bgR * (1 - alpha)),
        Math.floor(iconPixel[1] * alpha + bgG * (1 - alpha)),
        Math.floor(iconPixel[2] * alpha + bgB * (1 - alpha)),
        255
      ];
    }
  }

  // Right content decorative divider
  if (x >= 490 && x <= 492 && y >= 100 && y <= 530) {
    return [227, 167, 47, 180]; // Gold line
  }

  // Subtle mandala dots in corners
  const cornerDist = Math.hypot(x - (w - 100), y - 100);
  if (cornerDist < 80 && Math.floor(cornerDist) % 12 === 0) {
    return [227, 167, 47, 70];
  }

  // Quality badge box (bottom right)
  if (x >= 520 && x <= 1120 && y >= 450 && y <= 510) {
    // Terracotta pill badge
    return [164, 85, 58, 210];
  }

  return [bgR, bgG, bgB, 255];
}

// -------------------------------------------------------------
// MAIN GENERATION ROUTINE
// -------------------------------------------------------------

function run() {
  console.log('Generating Brand Assets & Logos for The Great Ayurveda...');

  const webImagesDir = path.resolve(__dirname, '../src/Ayurveda.Web/wwwroot/images');
  const webRoot = path.resolve(__dirname, '../src/Ayurveda.Web/wwwroot');
  const adminImagesDir = path.resolve(__dirname, '../src/Ayurveda.Admin/wwwroot/images');
  const adminRoot = path.resolve(__dirname, '../src/Ayurveda.Admin/wwwroot');

  fs.mkdirSync(webImagesDir, { recursive: true });
  fs.mkdirSync(adminImagesDir, { recursive: true });

  // 1. Write SVG Logos
  const logoMarkSvg = getLogoMarkSvg();
  const logoFullSvg = getLogoFullSvg();
  const logoDarkSvg = getLogoDarkSvg();
  const adminLogoFullSvg = getAdminLogoFullSvg();
  const adminLogoDarkSvg = getAdminLogoDarkSvg();
  const adminLogoMarkSvg = getAdminLogoMarkSvg();

  // Storefront SVGs
  fs.writeFileSync(path.join(webImagesDir, 'logo-mark.svg'), logoMarkSvg, 'utf8');
  fs.writeFileSync(path.join(webImagesDir, 'logo-full.svg'), logoFullSvg, 'utf8');
  fs.writeFileSync(path.join(webImagesDir, 'logo-dark.svg'), logoDarkSvg, 'utf8');
  fs.writeFileSync(path.join(webImagesDir, 'logo.svg'), logoFullSvg, 'utf8'); // default alias

  // Admin SVGs
  fs.writeFileSync(path.join(adminImagesDir, 'logo-mark.svg'), adminLogoMarkSvg, 'utf8');
  fs.writeFileSync(path.join(adminImagesDir, 'logo-full.svg'), adminLogoFullSvg, 'utf8');
  fs.writeFileSync(path.join(adminImagesDir, 'logo-dark.svg'), adminLogoDarkSvg, 'utf8');
  fs.writeFileSync(path.join(adminImagesDir, 'logo.svg'), adminLogoFullSvg, 'utf8');
  fs.writeFileSync(path.join(adminImagesDir, 'admin-logo-full.svg'), adminLogoFullSvg, 'utf8');

  console.log('SVGs written successfully.');

  // 2. Generate Storefront Raster Favicons & Icons
  console.log('Rendering Storefront PNGs & ICOs...');
  const png16 = createPng(16, 16, (x, y, w, h) => renderBrandIconPixel(x, y, w, h));
  const png32 = createPng(32, 32, (x, y, w, h) => renderBrandIconPixel(x, y, w, h));
  const png48 = createPng(48, 48, (x, y, w, h) => renderBrandIconPixel(x, y, w, h));
  const png180 = createPng(180, 180, (x, y, w, h) => renderBrandIconPixel(x, y, w, h));
  const png192 = createPng(192, 192, (x, y, w, h) => renderBrandIconPixel(x, y, w, h));
  const png512 = createPng(512, 512, (x, y, w, h) => renderBrandIconPixel(x, y, w, h));
  const pngMaskable512 = createPng(512, 512, (x, y, w, h) => renderBrandIconPixel(x, y, w, h, { isMaskable: true }));

  const ico = buildIco([png16, png32, png48], [16, 32, 48]);

  fs.writeFileSync(path.join(webRoot, 'favicon.ico'), ico);
  fs.writeFileSync(path.join(webRoot, 'favicon-32.png'), png32);
  fs.writeFileSync(path.join(webRoot, 'favicon-32x32.png'), png32);
  fs.writeFileSync(path.join(webRoot, 'apple-touch-icon.png'), png180);
  fs.writeFileSync(path.join(webRoot, 'android-chrome-192x192.png'), png192);
  fs.writeFileSync(path.join(webRoot, 'android-chrome-512x512.png'), png512);
  fs.writeFileSync(path.join(webRoot, 'android-chrome-maskable-512x512.png'), pngMaskable512);

  // 3. Generate OG Image (1200 x 630)
  console.log('Rendering 1200x630 og-image.png...');
  const ogPng = createPng(1200, 630, (x, y, w, h) => renderOgImagePixel(x, y, w, h));
  fs.writeFileSync(path.join(webRoot, 'og-image.png'), ogPng);

  // 4. Web Manifest for Storefront
  const webManifest = {
    name: 'The Great Ayurveda',
    short_name: 'Great Ayurveda',
    description: 'Authentic Ayurvedic Care, Classical Formulations & Doctor Consultation',
    start_url: '/',
    display: 'standalone',
    background_color: '#FBF6EA',
    theme_color: '#1F5E3B',
    icons: [
      {
        src: '/android-chrome-192x192.png',
        sizes: '192x192',
        type: 'image/png'
      },
      {
        src: '/android-chrome-512x512.png',
        sizes: '512x512',
        type: 'image/png',
        purpose: 'any'
      },
      {
        src: '/android-chrome-maskable-512x512.png',
        sizes: '512x512',
        type: 'image/png',
        purpose: 'maskable'
      }
    ]
  };
  fs.writeFileSync(path.join(webRoot, 'site.webmanifest'), JSON.stringify(webManifest, null, 2), 'utf8');

  // 5. Generate Admin Portal Favicons & Icons
  console.log('Rendering Admin Portal PNGs & ICOs...');
  const adminPng16 = createPng(16, 16, (x, y, w, h) => renderBrandIconPixel(x, y, w, h, { isAdmin: true }));
  const adminPng32 = createPng(32, 32, (x, y, w, h) => renderBrandIconPixel(x, y, w, h, { isAdmin: true }));
  const adminPng48 = createPng(48, 48, (x, y, w, h) => renderBrandIconPixel(x, y, w, h, { isAdmin: true }));
  const adminPng180 = createPng(180, 180, (x, y, w, h) => renderBrandIconPixel(x, y, w, h, { isAdmin: true }));
  const adminIco = buildIco([adminPng16, adminPng32, adminPng48], [16, 32, 48]);

  fs.writeFileSync(path.join(adminRoot, 'favicon.ico'), adminIco);
  fs.writeFileSync(path.join(adminRoot, 'favicon-32.png'), adminPng32);
  fs.writeFileSync(path.join(adminRoot, 'favicon-32x32.png'), adminPng32);
  fs.writeFileSync(path.join(adminRoot, 'apple-touch-icon.png'), adminPng180);

  const adminManifest = {
    name: 'The Great Ayurveda Admin Portal',
    short_name: 'TGA Admin',
    description: 'Administration & Doctor Verification Portal for The Great Ayurveda',
    start_url: '/',
    display: 'standalone',
    background_color: '#111814',
    theme_color: '#18221D',
    icons: [
      {
        src: '/favicon-32x32.png',
        sizes: '32x32',
        type: 'image/png'
      },
      {
        src: '/apple-touch-icon.png',
        sizes: '180x180',
        type: 'image/png'
      }
    ]
  };
  fs.writeFileSync(path.join(adminRoot, 'admin.webmanifest'), JSON.stringify(adminManifest, null, 2), 'utf8');

  console.log('All branding assets generated successfully!');
}

run();
