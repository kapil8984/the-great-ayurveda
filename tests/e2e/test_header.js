const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const SCREENSHOT_DIR = path.resolve(__dirname, 'screenshots');
if (!fs.existsSync(SCREENSHOT_DIR)) {
    fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
}

let passed = 0;
let failed = 0;

function assert(condition, message) {
    if (condition) {
        console.log(`✅ PASS: ${message}`);
        passed++;
    } else {
        console.error(`❌ FAIL: ${message}`);
        failed++;
    }
}

async function run() {
    console.log('================================================================');
    console.log('🧪 GROUP 1: HEADER & STICKY NAVIGATION VERIFICATION');
    console.log('================================================================\n');

    const browser = await chromium.launch({ headless: true });

    // Test across all required viewports: 1366, 1536, 1920, and 390 (mobile)
    const viewports = [
        { width: 1366, height: 768, name: '1366x768' },
        { width: 1536, height: 864, name: '1536x864' },
        { width: 1920, height: 1080, name: '1920x1080' },
        { width: 390, height: 844, name: '390x844_mobile', isMobile: true }
    ];

    for (const vp of viewports) {
        console.log(`\n--- Testing Viewport: ${vp.name} ---`);
        const context = await browser.newContext({ viewport: { width: vp.width, height: vp.height } });
        const page = await context.newPage();

        await page.goto('http://localhost:5000/', { waitUntil: 'networkidle', timeout: 20000 });
        await page.waitForTimeout(500);

        // 1. Measure header dimensions
        const headerInfo = await page.evaluate(() => {
            const wrapper = document.getElementById('tgaSiteHeaderWrapper');
            const topBar = document.getElementById('tgaTopBar');
            const navRow = document.querySelector('.tga-header-nav-row');
            const row1 = document.querySelector('.tga-header-row-1');
            return {
                wrapperHeight: wrapper ? wrapper.getBoundingClientRect().height : 0,
                topBarHeight: topBar ? topBar.getBoundingClientRect().height : 0,
                navRowHeight: navRow ? navRow.getBoundingClientRect().height : 0,
                row1Height: row1 ? row1.getBoundingClientRect().height : 0,
                topBarText: topBar ? topBar.innerText : '',
                navText: navRow ? navRow.innerText : ''
            };
        });

        console.log(`Viewport ${vp.name} measured heights:`, {
            totalHeaderHeight: `${headerInfo.wrapperHeight.toFixed(1)}px`,
            topBarHeight: `${headerInfo.topBarHeight.toFixed(1)}px`,
            row1Height: `${headerInfo.row1Height.toFixed(1)}px`,
            navRowHeight: `${headerInfo.navRowHeight.toFixed(1)}px`
        });

        // Top bar verification
        assert(Math.round(headerInfo.topBarHeight) <= 34, `Top offers strip height is slim (~32px) [Actual: ${headerInfo.topBarHeight.toFixed(1)}px]`);
        assert(!headerInfo.topBarText.includes('Senior BAMS Vaidyas'), 'Top bar removed "Senior BAMS Vaidyas" wording');
        assert(headerInfo.topBarText.includes('HEALTH15') || headerInfo.topBarText.includes('DIWALI30'), 'Top bar contains offer marquee & coupons');

        if (!vp.isMobile) {
            // Desktop full-screen: header height must be 110px or less
            assert(headerInfo.wrapperHeight <= 112, `Total header height is 110px or less on ${vp.name} [Actual: ${headerInfo.wrapperHeight.toFixed(1)}px]`);

            // Row 2 fits on one line (navRow height <= 36px)
            assert(headerInfo.navRowHeight <= 36, `Nav row fits on single line with no wrapping on ${vp.name} [Actual: ${headerInfo.navRowHeight.toFixed(1)}px]`);

            // Check "More" dropdown exists
            const moreDropdown = await page.$('.tga-header-nav-row .dropdown:has-text("More")');
            assert(moreDropdown !== null, `Nav row contains "More" dropdown for extra links on ${vp.name}`);
        } else {
            // Mobile: check offcanvas drawer
            const drawerBtn = await page.$('button[data-bs-target="#mobileMenuDrawer"]');
            assert(drawerBtn !== null, 'Mobile hamburger button is present');
            await drawerBtn.click();
            await page.waitForTimeout(600);

            const isDrawerOpen = await page.evaluate(() => {
                const drawer = document.getElementById('mobileMenuDrawer');
                return drawer && drawer.classList.contains('show');
            });
            assert(isDrawerOpen, 'Mobile offcanvas drawer opens smoothly');

            const drawerCloseBtn = await page.$('#mobileMenuDrawer button.btn-close');
            if (drawerCloseBtn) {
                await drawerCloseBtn.click();
                await page.waitForTimeout(400);
            }
        }

        // Test Smart Sticky Header (scroll down hides, scroll up reveals)
        if (!vp.isMobile) {
            console.log('Testing smart sticky scroll animation...');
            // Scroll down
            await page.evaluate(() => window.scrollTo({ top: 350, behavior: 'instant' }));
            await page.waitForTimeout(400);

            const isHiddenOnScrollDown = await page.evaluate(() => {
                const wrapper = document.getElementById('tgaSiteHeaderWrapper');
                return wrapper && wrapper.classList.contains('header-hidden');
            });
            assert(isHiddenOnScrollDown, `Header slides up and hides when scrolling DOWN on ${vp.name}`);

            // Scroll up
            await page.evaluate(() => window.scrollTo({ top: 150, behavior: 'instant' }));
            await page.waitForTimeout(400);

            const isRevealedOnScrollUp = await page.evaluate(() => {
                const wrapper = document.getElementById('tgaSiteHeaderWrapper');
                return wrapper && !wrapper.classList.contains('header-hidden');
            });
            assert(isRevealedOnScrollUp, `Header smoothly comes back when scrolling UP on ${vp.name}`);

            // Reset scroll to top
            await page.evaluate(() => window.scrollTo({ top: 0, behavior: 'instant' }));
            await page.waitForTimeout(400);
        }

        const screenshotPath = path.join(SCREENSHOT_DIR, `header_${vp.name}.png`);
        await page.screenshot({ path: screenshotPath, fullPage: false });
        console.log(`📸 Screenshot saved: ${screenshotPath}`);

        await context.close();
    }

    await browser.close();

    console.log('\n================================================================');
    console.log(`TEST SUMMARY: Passed: ${passed}, Failed: ${failed}`);
    console.log('================================================================');

    if (failed > 0) {
        process.exit(1);
    }
}

run().catch(err => {
    console.error('Test execution failed:', err);
    process.exit(1);
});
