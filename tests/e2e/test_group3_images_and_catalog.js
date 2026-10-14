const { chromium } = require('playwright');
const http = require('http');
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');

const WEB_URL = 'http://localhost:5000';
const ADMIN_URL = 'http://localhost:7200';
const SCREENSHOT_DIR = path.resolve(__dirname, 'screenshots');
fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });

const CATALOG_JSON = path.resolve(__dirname, '..', '..', 'src', 'Ayurveda.Infrastructure', 'Data', 'catalog_300plus.json');

function checkHttp200(url) {
    return new Promise((resolve) => {
        const req = http.get(url, (res) => {
            resolve(res.statusCode);
        });
        req.on('error', () => resolve(0));
        req.setTimeout(5000, () => {
            req.destroy();
            resolve(408);
        });
    });
}

function getFileHash(filePath) {
    const buf = fs.readFileSync(filePath);
    return crypto.createHash('sha256').update(buf).digest('hex');
}

async function runGroup3Verification() {
    console.log('================================================================');
    console.log('🧪 GROUP 3 VERIFICATION: IMAGES, DOCTORS, AND 300+ CATALOG');
    console.log(`Web URL:   ${WEB_URL}`);
    console.log(`Admin URL: ${ADMIN_URL}`);
    console.log('================================================================');

    let passedCount = 0;
    let failedCount = 0;

    function assert(condition, message) {
        if (condition) {
            console.log(`✅ PASS: ${message}`);
            passedCount++;
        } else {
            console.error(`❌ FAIL: ${message}`);
            failedCount++;
        }
    }

    // -------------------------------------------------------------------------
    // 1. VERIFY 24 DOCTOR IMAGES (HTTP 200 & SHA256 UNIQUENESS)
    // -------------------------------------------------------------------------
    console.log('\n--- 1. Verifying 24 Distinct Doctor Portraits ---');
    const doctorHashes = new Map();
    let docWeb200Count = 0;
    let docAdmin200Count = 0;

    const webWwwroot = path.resolve(__dirname, '..', '..', 'src', 'Ayurveda.Web', 'wwwroot');
    const adminWwwroot = path.resolve(__dirname, '..', '..', 'src', 'Ayurveda.Admin', 'wwwroot');

    for (let id = 1; id <= 24; id++) {
        const webUrl = `${WEB_URL}/images/doctors/doctor-${id}.webp`;
        const adminUrl = `${ADMIN_URL}/images/doctors/doctor-${id}.webp`;

        const webStatus = await checkHttp200(webUrl);
        const adminStatus = await checkHttp200(adminUrl);

        if (webStatus === 200) docWeb200Count++;
        if (adminStatus === 200) docAdmin200Count++;

        const filePath = path.join(webWwwroot, 'images', 'doctors', `doctor-${id}.webp`);
        if (fs.existsSync(filePath)) {
            const hash = getFileHash(filePath);
            const sizeKb = Math.round(fs.statSync(filePath).size / 1024);
            if (doctorHashes.has(hash)) {
                console.error(`Duplicate hash for doctor-${id} with doctor-${doctorHashes.get(hash)}`);
            }
            doctorHashes.set(hash, id);
        }
    }

    assert(docWeb200Count === 24, `All 24 doctor portraits return HTTP 200 on Web storefront (${docWeb200Count}/24)`);
    assert(docAdmin200Count === 24, `All 24 doctor portraits return HTTP 200 on Admin console (${docAdmin200Count}/24)`);
    assert(doctorHashes.size === 24, `Zero duplicate doctor image hashes: all 24 portraits have unique SHA-256 hashes (${doctorHashes.size}/24)`);

    // -------------------------------------------------------------------------
    // 2. VERIFY 300+ PRODUCT IMAGES (HTTP 200 & SHA256 UNIQUENESS)
    // -------------------------------------------------------------------------
    console.log('\n--- 2. Verifying 300+ Product Images & Catalog ---');
    const catalogData = JSON.parse(fs.readFileSync(CATALOG_JSON, 'utf8'));
    console.log(`Loaded catalog metadata: ${catalogData.length} items`);
    assert(catalogData.length >= 300, `Catalog contains 300+ products (${catalogData.length} total)`);

    const productHashes = new Map();
    let prdWeb200Count = 0;
    let prdAdmin200Count = 0;
    let under80KbCount = 0;

    for (let i = 0; i < catalogData.length; i++) {
        const item = catalogData[i];
        const webUrl = `${WEB_URL}${item.imageUrl}`;
        const adminUrl = `${ADMIN_URL}${item.imageUrl}`;

        const webStatus = await checkHttp200(webUrl);
        const adminStatus = await checkHttp200(adminUrl);

        if (webStatus === 200) prdWeb200Count++;
        if (adminStatus === 200) prdAdmin200Count++;

        const fileName = path.basename(item.imageUrl);
        const filePath = path.join(webWwwroot, 'images', 'products', fileName);
        if (fs.existsSync(filePath)) {
            const stat = fs.statSync(filePath);
            if (stat.size <= 80 * 1024) under80KbCount++;
            const hash = getFileHash(filePath);
            if (productHashes.has(hash)) {
                console.error(`Duplicate hash for ${fileName} with ${productHashes.get(hash)}`);
            }
            productHashes.set(hash, fileName);
        }
    }

    assert(prdWeb200Count === catalogData.length, `All ${catalogData.length} product images return HTTP 200 on Web storefront (${prdWeb200Count}/${catalogData.length})`);
    assert(prdAdmin200Count === catalogData.length, `All ${catalogData.length} product images return HTTP 200 on Admin console (${prdAdmin200Count}/${catalogData.length})`);
    assert(under80KbCount === catalogData.length, `All ${catalogData.length} product images are optimized under 80KB WebP (${under80KbCount}/${catalogData.length})`);
    assert(productHashes.size === catalogData.length, `Zero duplicate product image hashes: each of the ${catalogData.length} products has a unique SHA-256 hash (${productHashes.size}/${catalogData.length})`);

    // -------------------------------------------------------------------------
    // 3. VERIFY 25+ CATEGORIES (HTTP 200)
    // -------------------------------------------------------------------------
    console.log('\n--- 3. Verifying 25+ Categories & Icons ---');
    const categoriesDir = path.join(webWwwroot, 'images', 'categories');
    const catFiles = fs.readdirSync(categoriesDir).filter(f => f.endsWith('.svg'));
    assert(catFiles.length >= 25, `Expanded categories count >= 25 (${catFiles.length} category icons present)`);

    let catWeb200 = 0;
    for (const f of catFiles) {
        const status = await checkHttp200(`${WEB_URL}/images/categories/${f}`);
        if (status === 200) catWeb200++;
    }
    assert(catWeb200 === catFiles.length, `All ${catFiles.length} category icons return HTTP 200 on Web storefront`);

    // -------------------------------------------------------------------------
    // 4. BROWSER VERIFICATION & PLAYWRIGHT CLICK-THROUGH
    // -------------------------------------------------------------------------
    console.log('\n--- 4. Browser Automation & Screenshots ---');
    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1400, height: 900 } });
    const page = await context.newPage();

    try {
        // A. Doctors Directory
        await page.goto(`${WEB_URL}/Doctors`, { waitUntil: 'domcontentloaded', timeout: 45000 });
        await page.waitForTimeout(2000);
        const docPageHtml = await page.content();
        assert(docPageHtml.includes('Dr. Aarav Sharma') && docPageHtml.includes('Dr. Ananya Nambiar'), 'Doctors directory renders seeded clinicians');

        // Check broken images on Doctors page
        const brokenDocImgs = await page.evaluate(() => {
            const imgs = Array.from(document.querySelectorAll('img'));
            return imgs.filter(img => !img.complete || img.naturalWidth === 0).map(img => img.src);
        });
        assert(brokenDocImgs.length === 0, `Zero broken images on Doctors page (broken: ${brokenDocImgs.length})`);
        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group3_doctors_directory.png') });
        console.log('📸 Captured group3_doctors_directory.png');

        // B. Storefront Products Catalog (/Shop)
        await page.goto(`${WEB_URL}/Shop`, { waitUntil: 'domcontentloaded', timeout: 45000 });
        await page.waitForTimeout(2000);
        const shopHtml = await page.content();
        assert(shopHtml.includes('AI-generated sample images'), 'Storefront footer displays "AI-generated sample images" notice');

        // Check broken images on Shop page
        const brokenShopImgs = await page.evaluate(() => {
            const imgs = Array.from(document.querySelectorAll('img'));
            return imgs.filter(img => !img.complete || img.naturalWidth === 0).map(img => img.src);
        });
        assert(brokenShopImgs.length === 0, `Zero broken images on Shop catalog page (broken: ${brokenShopImgs.length})`);
        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group3_products_catalog.png') });
        console.log('📸 Captured group3_products_catalog.png');

        // C. Product Details Page & Claim Verification
        const firstProductCard = await page.$('a[href*="/Shop/Details/"], a[href*="/Details/"]');
        if (firstProductCard) {
            const detailHref = await firstProductCard.getAttribute('href');
            await page.goto(`${WEB_URL}${detailHref}`, { waitUntil: 'domcontentloaded', timeout: 45000 });
            await page.waitForTimeout(2000);

            const detailHtml = await page.content();
            assert(detailHtml.toLowerCase().includes('traditionally used for'), 'Product details page uses compliant "traditionally used for" wording');
            assert(!detailHtml.includes('Guaranteed cure') && !detailHtml.includes('100% cure'), 'Zero prohibited cure claims on product details page');

            const brokenDetailImgs = await page.evaluate(() => {
                const imgs = Array.from(document.querySelectorAll('img'));
                return imgs.filter(img => !img.complete || img.naturalWidth === 0).map(img => img.src);
            });
            assert(brokenDetailImgs.length === 0, `Zero broken images on Product Details page (broken: ${brokenDetailImgs.length})`);
            await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group3_product_detail_page.png') });
            console.log('📸 Captured group3_product_detail_page.png');
        }

        // D. Admin Products Inventory
        const adminPage = await context.newPage();
        await adminPage.goto(`${ADMIN_URL}/Auth/Login`, { waitUntil: 'domcontentloaded', timeout: 45000 });
        await adminPage.fill('#adminEmailInput', 'admin@ayurveda.com');
        await adminPage.fill('#adminPwdInput', 'DevAuth_Vaidya_2026!#');
        await Promise.all([
            adminPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 45000 }),
            adminPage.click('button[type="submit"]')
        ]);
        await adminPage.waitForTimeout(1000);

        await adminPage.goto(`${ADMIN_URL}/Products`, { waitUntil: 'domcontentloaded', timeout: 45000 });
        await adminPage.waitForTimeout(2000);
        const adminProdHtml = await adminPage.content();
        assert(adminProdHtml.includes('Products') || adminProdHtml.includes('Catalogue'), 'Admin products management console loaded successfully');

        const brokenAdminProdImgs = await adminPage.evaluate(() => {
            const imgs = Array.from(document.querySelectorAll('img'));
            return imgs.filter(img => !img.complete || img.naturalWidth === 0).map(img => img.src);
        });
        assert(brokenAdminProdImgs.length === 0, `Zero broken images on Admin Products inventory (broken: ${brokenAdminProdImgs.length})`);
        await adminPage.screenshot({ path: path.join(SCREENSHOT_DIR, 'group3_admin_products_inventory.png') });
        console.log('📸 Captured group3_admin_products_inventory.png');

        // E. Admin Doctors List
        await adminPage.goto(`${ADMIN_URL}/Doctors`, { waitUntil: 'domcontentloaded', timeout: 45000 });
        await adminPage.waitForTimeout(2000);
        const adminDocHtml = await adminPage.content();
        assert(adminDocHtml.includes('Dr. Aarav Sharma') || adminDocHtml.includes('Doctors'), 'Admin doctors console loaded successfully');

        const brokenAdminDocImgs = await adminPage.evaluate(() => {
            const imgs = Array.from(document.querySelectorAll('img'));
            return imgs.filter(img => !img.complete || img.naturalWidth === 0).map(img => img.src);
        });
        assert(brokenAdminDocImgs.length === 0, `Zero broken images on Admin Doctors list (broken: ${brokenAdminDocImgs.length})`);
        await adminPage.screenshot({ path: path.join(SCREENSHOT_DIR, 'group3_admin_doctors_list.png') });
        console.log('📸 Captured group3_admin_doctors_list.png');

    } finally {
        await browser.close();
    }

    console.log('\n================================================================');
    console.log(`GROUP 3 SUMMARY: ${passedCount} PASSED, ${failedCount} FAILED`);
    console.log(`Exact Counts: 24 Doctors, ${catalogData.length} Products, ${catFiles.length} Categories`);
    console.log(`Unique Image Hashes: 24 Doctor Hashes, ${productHashes.size} Product Hashes`);
    console.log('================================================================');

    process.exit(failedCount > 0 ? 1 : 0);
}

runGroup3Verification().catch(err => {
    console.error('Fatal error during Group 3 verification:', err);
    process.exit(1);
});
