const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');
const { getDevAdminPassword } = require('./getTestPassword');

const WEB_URL = 'http://localhost:5000';
const ADMIN_URL = 'http://localhost:7200';
const SCREENSHOT_DIR = path.resolve(__dirname, 'screenshots');

if (!fs.existsSync(SCREENSHOT_DIR)) {
    fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
}

let passedCount = 0;
let failedCount = 0;

function assert(condition, description) {
    if (condition) {
        console.log(`✅ PASS: ${description}`);
        passedCount++;
    } else {
        console.error(`❌ FAIL: ${description}`);
        failedCount++;
    }
}

async function run() {
    console.log('================================================================');
    console.log('🧪 GROUP 1 E2E VERIFICATION SUITE');
    console.log(`Web URL:   ${WEB_URL}`);
    console.log(`Admin URL: ${ADMIN_URL}`);
    console.log('================================================================\n');

    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();
    const request = context.request;

    try {
        // -------------------------------------------------------------
        // 1. PUBLIC SITE: CHECK NO "GO TO ADMIN" & HERO CAROUSEL MOTION
        // -------------------------------------------------------------
        console.log('\n--- 1. Testing Public Site & Hero Carousel Movement ---');
        await page.goto(`${WEB_URL}/`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await page.waitForTimeout(1000);

        const pageContent = await page.content();
        assert(!pageContent.includes('Go to Admin'), 'Public homepage contains NO "Go to Admin" link');

        // Check if hero carousel exists
        const carousel = await page.$('#homeHeroCarousel');
        assert(carousel !== null, 'Hero carousel #homeHeroCarousel exists on public homepage');

        // Check hero carousel moves after ~4.5 seconds
        const firstActiveSlide = await page.$eval('#homeHeroCarousel .carousel-item.active', el => el.innerHTML);
        console.log('Waiting 4.5 seconds to observe hero carousel auto-slide...');
        await page.waitForTimeout(4500);
        const secondActiveSlide = await page.$eval('#homeHeroCarousel .carousel-item.active', el => el.innerHTML);
        
        assert(firstActiveSlide !== secondActiveSlide, 'Hero carousel automatically advanced to the next slide (~4s)');
        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group1_hero_carousel_movement.png') });
        console.log('📸 Captured group1_hero_carousel_movement.png');

        // -------------------------------------------------------------
        // 2. ADMIN SITE: LOGIN & CHECK "VIEW WEBSITE" BUTTONS
        // -------------------------------------------------------------
        console.log('\n--- 2. Testing Admin Login & "View Website" Links ---');
        await page.goto(`${ADMIN_URL}/Auth/Login`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await page.fill('#adminEmailInput', 'admin@ayurveda.com');
        await page.fill('#adminPwdInput', getDevAdminPassword());
        await Promise.all([
            page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }).catch(e => console.log('Navigation event note:', e.message)),
            page.click('button[type="submit"]')
        ]);
        await page.waitForTimeout(2000);
        console.log('Current URL after login attempt:', page.url());
        const loginErr = await page.$('.alert-danger');
        if (loginErr) {
            console.log('Login error message on page:', await loginErr.textContent());
        }
        assert(page.url().includes('localhost:7200') && !page.url().includes('/Auth/Login'), 'Admin logged in successfully and reached dashboard');

        // Check "View Website" buttons in header and sidebar
        const viewWebsiteHeader = await page.$('header a[href*="localhost:5000"][target="_blank"], nav a[href*="localhost:5000"][target="_blank"], a.btn:has-text("View Website")');
        assert(viewWebsiteHeader !== null, 'Admin header contains visible "View Website" button targeting public site in new tab');

        const viewWebsiteSidebar = await page.$('aside a[href*="localhost:5000"], nav.sidebar a[href*="localhost:5000"], .sidebar a:has-text("View Website")');
        assert(viewWebsiteSidebar !== null, 'Admin sidebar contains visible "View Website" button');

        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group1_admin_view_website_buttons.png') });
        console.log('📸 Captured group1_admin_view_website_buttons.png');

        // -------------------------------------------------------------
        // 3. ADMIN BANNERS & REAL COUNTS
        // -------------------------------------------------------------
        console.log('\n--- 3. Testing Admin Banners & Real Counts ---');
        await page.goto(`${ADMIN_URL}/Marketing/Banners`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await page.waitForTimeout(1000);

        const bannerPageHtml = await page.content();
        assert(bannerPageHtml.includes('Total Banners'), 'Admin banner list displays Total Banners KPI');
        assert(bannerPageHtml.includes('Active') && bannerPageHtml.includes('Published'), 'Admin banner list displays Active & Published KPI');
        assert(bannerPageHtml.includes('Hero Carousel Slides'), 'Admin banner list displays Hero Carousel Slides KPI');

        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group1_admin_banners_kpi.png') });
        console.log('📸 Captured group1_admin_banners_kpi.png');

        // -------------------------------------------------------------
        // 4. SHARED UPLOADS: UPLOAD IN ADMIN -> APPEARS ON PUBLIC SITE
        // -------------------------------------------------------------
        console.log('\n--- 4. Testing Shared Uploads Folder (Admin Upload -> Public Site Served) ---');
        // Create a temporary test image file
        const testImgPath = path.join(__dirname, 'test_shared_upload.png');
        // A minimal valid 1x1 PNG in base64
        const samplePngBuffer = Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==', 'base64');
        fs.writeFileSync(testImgPath, samplePngBuffer);

        // Open Add Banner modal
        await page.click('button[data-bs-target="#createBannerModal"]');
        await page.waitForSelector('#createBannerModal input[name="title"]', { state: 'visible', timeout: 5000 });
        await page.fill('#createBannerModal input[name="title"]', 'E2E Proof Shared Upload Banner');
        await page.fill('#createBannerModal input[name="subtitle"]', 'Testing shared upload folder synchronization');
        await page.selectOption('#createBannerModal select[name="placement"]', 'home_hero');
        await page.fill('#createBannerModal input[name="displayOrder"]', '1');
        await page.setInputFiles('#bannerCreateFileInput', testImgPath);

        await Promise.all([
            page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }),
            page.click('#createBannerModal button[type="submit"]')
        ]);
        console.log('Submitted new banner with image upload.');

        // Find the newly uploaded image in the banners cards
        const imgSrc = await page.evaluate(() => {
            const imgs = Array.from(document.querySelectorAll('img'));
            const uploadImg = imgs.find(i => (i.getAttribute('src') || '').includes('/uploads/'));
            return uploadImg ? uploadImg.getAttribute('src') : null;
        });

        assert(imgSrc !== null && imgSrc.startsWith('/uploads/'), `Newly created banner has image starting with /uploads/ (${imgSrc})`);

        if (imgSrc) {
            console.log(`Uploaded image path: ${imgSrc}`);

            // Test 1: Fetch from Admin (http://localhost:7200/uploads/...)
            const adminResp = await request.get(`${ADMIN_URL}${imgSrc}`);
            assert(adminResp.status() === 200, `Image served by Admin at ${ADMIN_URL}${imgSrc} (HTTP 200)`);

            // Test 2: Fetch from Public Web (http://localhost:5000/uploads/...)
            const webResp = await request.get(`${WEB_URL}${imgSrc}`);
            assert(webResp.status() === 200, `SAME uploaded image served by Public Web at ${WEB_URL}${imgSrc} (HTTP 200)`);

            // Test 3: Load Public Homepage and verify the banner is rendered
            await page.goto(`${WEB_URL}/`, { waitUntil: 'domcontentloaded', timeout: 15000 });
            const publicBannerImg = await page.$(`img[src="${imgSrc}"]`);
            assert(publicBannerImg !== null, 'Public homepage renders the image uploaded from Admin');
            
            await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group1_shared_upload_on_public_site.png') });
            console.log('📸 Captured group1_shared_upload_on_public_site.png');
        }

        // Clean up temporary image
        if (fs.existsSync(testImgPath)) {
            fs.unlinkSync(testImgPath);
        }

        // -------------------------------------------------------------
        // 5. INVOICE: VERIFY DEMO INVOICE WITHOUT FAKE CLAIMS
        // -------------------------------------------------------------
        console.log('\n--- 5. Testing Invoice Claims Removal ---');
        await page.goto(`${ADMIN_URL}/Orders`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        const invoiceLink = await page.$('a[href*="/Invoice"]');
        if (invoiceLink) {
            const invoiceHref = await invoiceLink.getAttribute('href');
            await page.goto(`${ADMIN_URL}${invoiceHref}`, { waitUntil: 'domcontentloaded', timeout: 15000 });
            const invoiceHtml = await page.content();

            assert(invoiceHtml.includes('Demo invoice - sample data'), 'Invoice clearly displays "Demo invoice - sample data"');
            assert(!invoiceHtml.includes('DL-AYUR-8914'), 'Invoice has NO fake AYUSH license (DL-AYUR-8914 removed)');
            assert(!invoiceHtml.includes('07AAACT2026A1Z5'), 'Invoice has NO fake GSTIN (07AAACT2026A1Z5 removed)');

            await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group1_demo_invoice_verified.png') });
            console.log('📸 Captured group1_demo_invoice_verified.png');
        }

        console.log('\n================================================================');
        console.log(`SUMMARY: ${passedCount} PASSED, ${failedCount} FAILED`);
        console.log('================================================================');

    } catch (err) {
        console.error('Fatal error during test run:', err);
        failedCount++;
    } finally {
        await browser.close();
        process.exit(failedCount > 0 ? 1 : 0);
    }
}

run();
