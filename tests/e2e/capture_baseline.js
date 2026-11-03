const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const BASE_URL = 'http://localhost:5000';
const OUT_DIR = path.join(__dirname, 'screenshots', 'baseline');
fs.mkdirSync(OUT_DIR, { recursive: true });

const viewports = [
  { width: 390, height: 844, name: '390x844' },
  { width: 768, height: 1024, name: '768x1024' },
  { width: 1366, height: 768, name: '1366x768' }
];

async function run() {
  const browser = await chromium.launch({ headless: true });
  console.log('Capturing baseline screenshots...');

  for (const vp of viewports) {
    console.log(`Viewport: ${vp.name}`);
    const context = await browser.newContext({
      viewport: { width: vp.width, height: vp.height },
      deviceScaleFactor: 1
    });
    const page = await context.newPage();

    // 1. Home top
    await page.goto(`${BASE_URL}/`, { waitUntil: 'networkidle' });
    await page.screenshot({ path: path.join(OUT_DIR, `home_top_${vp.name}.png`) });

    // 2. Home middle
    await page.evaluate(() => window.scrollTo(0, 1200));
    await page.waitForTimeout(400);
    await page.screenshot({ path: path.join(OUT_DIR, `home_middle_${vp.name}.png`) });

    // 3. Shop
    await page.goto(`${BASE_URL}/shop`, { waitUntil: 'networkidle' });
    await page.screenshot({ path: path.join(OUT_DIR, `shop_${vp.name}.png`) });

    // 4. Product
    await page.goto(`${BASE_URL}/shop/details/308`, { waitUntil: 'networkidle' });
    await page.screenshot({ path: path.join(OUT_DIR, `product_${vp.name}.png`) });

    // 5. Cart
    await page.goto(`${BASE_URL}/cart`, { waitUntil: 'networkidle' });
    await page.screenshot({ path: path.join(OUT_DIR, `cart_${vp.name}.png`) });

    // 6. Doctors
    await page.goto(`${BASE_URL}/doctors`, { waitUntil: 'networkidle' });
    await page.screenshot({ path: path.join(OUT_DIR, `doctors_${vp.name}.png`) });

    // 7. Login
    await page.goto(`${BASE_URL}/account/login`, { waitUntil: 'networkidle' });
    await page.screenshot({ path: path.join(OUT_DIR, `login_${vp.name}.png`) });

    // 8. Register
    await page.goto(`${BASE_URL}/account/register`, { waitUntil: 'networkidle' });
    await page.screenshot({ path: path.join(OUT_DIR, `register_${vp.name}.png`) });

    // Admin login for admin pages
    const adminContext = await browser.newContext({
      viewport: { width: vp.width, height: vp.height },
      deviceScaleFactor: 1
    });
    const adminPage = await adminContext.newPage();
    await adminPage.goto(`${BASE_URL}/admin-portal/auth/login`, { waitUntil: 'networkidle' });
    await adminPage.fill('input[name="Email"], #adminEmail', 'admin@thegreatayurveda.com');
    await adminPage.fill('input[name="Password"], #adminPassword', 'Admin@123');
    await Promise.all([
      adminPage.waitForNavigation({ waitUntil: 'networkidle' }).catch(() => {}),
      adminPage.click('button[type="submit"]')
    ]);

    // 9. Admin Dashboard
    await adminPage.goto(`${BASE_URL}/admin-portal/dashboard`, { waitUntil: 'networkidle' });
    await adminPage.screenshot({ path: path.join(OUT_DIR, `admin_dashboard_${vp.name}.png`) });

    // 10. Admin Products
    await adminPage.goto(`${BASE_URL}/admin-portal/products`, { waitUntil: 'networkidle' });
    await adminPage.screenshot({ path: path.join(OUT_DIR, `admin_products_${vp.name}.png`) });

    await context.close();
    await adminContext.close();
  }

  await browser.close();
  console.log('Baseline capture complete in:', OUT_DIR);
}

run().catch(err => {
  console.error('Error during baseline capture:', err);
  process.exit(1);
});
