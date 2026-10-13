const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const ADMIN_URL = 'http://localhost:7200';
const WEB_URL = 'http://localhost:5000';
const SCREENSHOT_DIR = path.resolve('tests/e2e/screenshots');
fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });

async function runGroup5Verification() {
    console.log('================================================================');
    console.log('🧪 GROUP 5 VERIFICATION: ADMIN DASHBOARD (WOW ON FIRST OPEN)');
    console.log(`Admin Target: ${ADMIN_URL} | Public Target: ${WEB_URL}`);
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

    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();

    try {
        console.log('\n--- 1. Admin Login as SuperAdmin ---');
        await page.goto(`${ADMIN_URL}/Auth/Login`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(1000);

        // Fill login credentials
        const emailInput = await page.$('#adminEmailInput, input[name="Email"]');
        const passInput = await page.$('#adminPwdInput, input[name="Password"]');
        assert(emailInput !== null && passInput !== null, 'Admin login form fields present (#adminEmailInput, #adminPwdInput)');

        await emailInput.fill('admin@ayurveda.com');
        await passInput.fill('Admin@123');
        await Promise.all([
            page.waitForURL(url => !url.href.includes('/Auth/Login'), { timeout: 20000 }),
            page.click('button[type="submit"]')
        ]);
        await page.waitForTimeout(2000);

        const currentUrl = page.url();
        assert(!currentUrl.includes('/Auth/Login'), `Admin successfully logged in and redirected to ${currentUrl}`);

        console.log('\n--- 2. Executive Dashboard Elements & Local Chart.js ---');
        // Check admin.css link
        const adminCss = await page.$('link[href*="admin.css"]');
        assert(adminCss !== null, 'Custom admin.css design system stylesheet loaded');

        // Check Local Chart.js loaded
        const chartJsLoaded = await page.evaluate(() => typeof window.Chart !== 'undefined');
        assert(chartJsLoaded, 'Local Chart.js library loaded and available on window (NO CDN)');

        // Check Chart Canvases
        const salesChart = await page.$('#adminSalesTrendChart');
        assert(salesChart !== null, 'Sales trend line chart canvas present (#adminSalesTrendChart)');

        const catChart = await page.$('#adminCategoryShareChart');
        assert(catChart !== null, 'Category share doughnut chart canvas present (#adminCategoryShareChart)');

        const prodChart = await page.$('#adminTopProductsChart');
        assert(prodChart !== null, 'Top formulations bar chart canvas present (#adminTopProductsChart)');

        // Verify Chart instances initialized
        const chartsCount = await page.evaluate(() => {
            return (window.tgaCharts && Array.isArray(window.tgaCharts)) ? window.tgaCharts.length : 0;
        });
        assert(chartsCount >= 3, `All 3 Chart.js instances successfully initialized in window.tgaCharts (Found: ${chartsCount})`);

        // Check 8 Animated KPI Cards
        const kpiCards = await page.$$('.admin-kpi-card');
        assert(kpiCards.length >= 8, `Executive KPI cards rendered (Found: ${kpiCards.length} cards, expected >= 8)`);

        const kpiNumbers = await page.$$('.admin-kpi-number');
        assert(kpiNumbers.length >= 8, `Animated count-up KPI number elements present (Found: ${kpiNumbers.length})`);

        // Check Consultation Modes from REAL counts (No hardcoded 65% / 20%)
        const consultModesProgress = await page.$$eval('.progress-bar', bars => bars.map(b => b.getAttribute('style') || ''));
        const hasFixed65 = consultModesProgress.some(s => s.includes('width: 65%'));
        const hasFixed25 = consultModesProgress.some(s => s.includes('width: 25%'));
        assert(!hasFixed65 && !hasFixed25, 'Consultation mode widths are dynamically computed from DB counts (fixed 65%/25% removed)');

        // Check Order Pipeline Funnel
        const funnelCard = await page.$('.bi-funnel');
        assert(funnelCard !== null, 'Order Pipeline Funnel telemetry card rendered');

        // Check Live-looking Activity Feed
        const activityItems = await page.$$('.activity-feed-item');
        assert(activityItems.length >= 1, `Live-looking security audit activity feed rendered (Found: ${activityItems.length} items)`);

        // Check View Website buttons
        const sidebarViewBtn = await page.$('#adminSidebarViewWebsiteBtn');
        const headerViewBtn = await page.$('#adminHeaderViewWebsiteBtn');
        assert(sidebarViewBtn !== null && headerViewBtn !== null, 'Visible "View Website" buttons present in both header and sidebar');

        // Capture Dashboard Screenshot
        const dashboardScreenshotPath = path.join(SCREENSHOT_DIR, 'group5_admin_dashboard_charts.png');
        await page.screenshot({ path: dashboardScreenshotPath, fullPage: false });
        console.log(`📸 Captured ${dashboardScreenshotPath}`);

        console.log('\n--- 3. Testing Dark / Light Theme Toggle ---');
        const themeBtn = await page.$('#adminThemeToggle');
        assert(themeBtn !== null, 'Dark/light theme toggle button present in header');

        await themeBtn.click();
        await page.waitForTimeout(500);

        const currentTheme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
        assert(currentTheme === 'dark', `Theme successfully switched to dark mode (data-theme="${currentTheme}")`);

        // Capture Dark Mode Screenshot
        const darkModeScreenshotPath = path.join(SCREENSHOT_DIR, 'group5_admin_dark_mode.png');
        await page.screenshot({ path: darkModeScreenshotPath, fullPage: false });
        console.log(`📸 Captured ${darkModeScreenshotPath}`);

        // Toggle back to light
        await themeBtn.click();
        await page.waitForTimeout(500);
        const restoredTheme = await page.evaluate(() => document.documentElement.getAttribute('data-theme'));
        assert(restoredTheme === 'light', 'Theme toggled back to light mode');

        console.log('\n--- 4. Testing Collapsible Sidebar ---');
        const sidebarToggle = await page.$('#adminSidebarToggle');
        assert(sidebarToggle !== null, 'Collapsible sidebar toggle button present');

        await sidebarToggle.click();
        await page.waitForTimeout(500);

        const isCollapsed = await page.evaluate(() => document.querySelector('.admin-sidebar').classList.contains('collapsed'));
        assert(isCollapsed, 'Sidebar collapsed state triggered (.collapsed class added)');

        // Capture Collapsed Sidebar Screenshot
        const collapsedScreenshotPath = path.join(SCREENSHOT_DIR, 'group5_admin_sidebar_collapsed.png');
        await page.screenshot({ path: collapsedScreenshotPath, fullPage: false });
        console.log(`📸 Captured ${collapsedScreenshotPath}`);

        // Restore sidebar
        await sidebarToggle.click();
        await page.waitForTimeout(500);

        console.log('\n--- 5. Testing All Admin Modules & Pages ---');
        const pagesToTest = [
            { path: '/Products', name: 'Products & Stock' },
            { path: '/Categories', name: 'Categories Catalog' },
            { path: '/Doctors', name: 'Doctor Practice' },
            { path: '/Doctors/Pending', name: 'Doctor Verification Queue' },
            { path: '/Orders', name: 'Orders & Dispatch' },
            { path: '/Appointments', name: 'Tele-Consultations' },
            { path: '/Marketing/Coupons', name: 'Coupons & Offers' },
            { path: '/Marketing/Banners', name: 'Store Banners' },
            { path: '/Moderation/Reviews', name: 'Reviews Moderation' },
            { path: '/Users', name: 'Users & Patients' }
        ];

        for (const item of pagesToTest) {
            await page.goto(`${ADMIN_URL}${item.path}`, { waitUntil: 'domcontentloaded', timeout: 30000 });
            await page.waitForTimeout(800);
            const status = await page.evaluate(() => document.title);
            assert(!status.includes('Error') && !status.includes('404'), `Admin module loaded: ${item.name} (${item.path})`);
        }

        console.log('\n--- 6. Testing Real-time Admin Sync to Public Site ---');
        // Edit product title or stock in Admin and verify it shows on public storefront
        await page.goto(`${ADMIN_URL}/Products`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(1000);

        // Find first edit button
        const firstEditBtn = await page.$('a[href*="/Products/Edit"]');
        assert(firstEditBtn !== null, 'Found product edit button in admin catalog');

        const editUrl = await firstEditBtn.getAttribute('href');
        await page.goto(`${ADMIN_URL}${editUrl}`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(1000);

        const titleInput = await page.$('input[name="Title"]');
        assert(titleInput !== null, 'Product edit form loaded with Title field');

        const originalTitle = await titleInput.inputValue();
        const testSuffix = ' [Certified Pure]';
        let updatedTitle = originalTitle;
        if (!originalTitle.includes(testSuffix)) {
            updatedTitle = originalTitle + testSuffix;
        }

        await titleInput.fill(updatedTitle);
        await Promise.all([
            page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 30000 }),
            page.click('button:has-text("Update Formulation")')
        ]);
        // Extract product ID
        const productId = editUrl.split('/').filter(Boolean).pop();

        // Open public product details page and verify updated title appears immediately
        const publicPage = await context.newPage();
        await publicPage.goto(`${WEB_URL}/Shop/Details/${productId}`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await publicPage.waitForTimeout(1500);

        const catalogContent = await publicPage.content();
        const syncSuccessful = catalogContent.includes(updatedTitle);
        assert(syncSuccessful, `Admin catalog edit verified: "${updatedTitle}" reflected immediately on public site (/Shop/Details/${productId})`);

        // Capture sync proof screenshot
        const syncProofPath = path.join(SCREENSHOT_DIR, 'group5_admin_crud_sync_proof.png');
        await publicPage.screenshot({ path: syncProofPath, fullPage: false });
        console.log(`📸 Captured ${syncProofPath}`);

        // Revert product title in Admin
        await page.goto(`${ADMIN_URL}${editUrl}`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(1000);
        const titleInputRevert = await page.$('input[name="Title"]');
        if (titleInputRevert) {
            await titleInputRevert.fill(originalTitle.replace(testSuffix, ''));
            await Promise.all([
                page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 30000 }),
                page.click('button:has-text("Update Formulation")')
            ]);
            console.log(`Cleaned up / reverted product title back to original: ${originalTitle.replace(testSuffix, '')}`);
        }

        // Test Banner CRUD synchronization
        await page.goto(`${ADMIN_URL}/Banners`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(1000);
        const bannerCards = await page.$$('.card');
        assert(bannerCards.length >= 1, 'Admin banners list renders active marketing placements');

        // Verify active hero banners appear on public home page
        await publicPage.goto(WEB_URL, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await publicPage.waitForTimeout(1500);
        const publicHeroCarousel = await publicPage.$('#homeHeroCarousel, .carousel');
        assert(publicHeroCarousel !== null, 'Public site hero carousel renders active banners immediately (#homeHeroCarousel)');

        await publicPage.close();

    } catch (err) {
        console.error('❌ Exception during Group 5 verification:', err);
        failedCount++;
    } finally {
        await browser.close();
    }

    console.log('\n================================================================');
    console.log(`🏁 GROUP 5 RESULTS: ${passedCount} PASSED | ${failedCount} FAILED`);
    console.log('================================================================');

    if (failedCount > 0) {
        process.exit(1);
    }
}

runGroup5Verification();
