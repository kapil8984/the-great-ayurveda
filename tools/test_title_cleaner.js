const fs = require('fs');
const path = require('path');

const catalog = JSON.parse(fs.readFileSync(path.join(__dirname, '..', 'src', 'Ayurveda.Infrastructure', 'Data', 'catalog_300plus.json'), 'utf-8'));

function cleanTitleForSearch(brand, title) {
  let clean = title;
  // Remove brand prefix if present
  clean = clean.replace(new RegExp(`^${brand}\\s*`, 'i'), '');
  clean = clean.replace(/^(Himalaya|Dabur|Patanjali|Baidyanath|Zandu|Charak|AVP|Sri Sri Tattva|Sri Sri|Maharishi Ayurveda|Kottakkal)\s*/i, '');
  // Remove brackets and parenthetical variations
  clean = clean.replace(/\([^)]*\)/g, '');
  // Remove pure herbs / tablet / syrup suffixes
  clean = clean.replace(/\b(Pure Herbs|Tablet|Syrup|Capsule|Capsules|Churna|Vati|Taila|Tailam|Oil|Ghritha|Asava|Arishta|Kwath|Ointment|Gel|Drops)\b/gi, (match) => {
    // Keep it if it's the core name, otherwise remove trailing
    return match;
  });
  clean = clean.replace(/\s+/g, ' ').trim();
  return clean;
}

console.log('Sample Cleaned Titles:');
catalog.slice(0, 30).forEach(p => {
  console.log(`${p.brand} | Original: "${p.title}" -> Cleaned: "${cleanTitleForSearch(p.brand, p.title)}"`);
});

