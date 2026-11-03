const fs = require('fs');
const path = require('path');

const catalogPath = path.join(__dirname, '..', 'src', 'Ayurveda.Infrastructure', 'Data', 'catalog_300plus.json');
const catalog = JSON.parse(fs.readFileSync(catalogPath, 'utf-8'));

console.log('Total products in catalog:', catalog.length);
const brands = {};
catalog.forEach(p => {
  brands[p.Brand] = (brands[p.Brand] || 0) + 1;
});

console.log('Brands breakdown:');
for (const [brand, count] of Object.entries(brands)) {
  console.log(`- ${brand}: ${count} products`);
}

