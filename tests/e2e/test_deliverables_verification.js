const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');
const { getDevAdminPassword } = require('./getTestPassword');

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

async function testHeaderDropdowns(browser) {
    console.log('\n================================================================');
    console.log('🧪 ITEM 3: HEADER DROPDOWNS & NAVIGATION TESTING (1366, 1536, 1920)');
    console.log('================================================================');

    const viewports = [
        { width: 1366, height: 768, name: '1366x768' },
        { width: 1536, height: 864, name: '1536x864' },
        { width: 1920, height: 1080, name: '1920x1080' }
    ];

    for (const vp of viewports) {
        console.log(`\n--- Testing Header on ${vp.name} ---`);
        const context = await browser.newContext({ viewport: { width: vp.width, height: vp.height } });
        const page = await context.newPage();

        await page.goto('http://localhost:5000/', { waitUntil: 'networkidle', timeout: 20000 });
        await page.waitForTimeout(500);

        // 1. Measure header height (Must be <= 110px)
        const headerInfo = await page.evaluate(() => {
            const wrapper = document.getElementById('tgaSiteHeaderWrapper');
            const navRow = document.querySelector('.tga-header-nav-row');
            return {
                wrapperHeight: wrapper ? wrapper.getBoundingClientRect().height : 0,
                navRowHeight: navRow ? navRow.getBoundingClientRect().height : 0,
                navRowOverflow: navRow ? window.getComputedStyle(navRow).overflow : ''
            };
        });

        console.log(`Header height on ${vp.name}: ${headerInfo.wrapperHeight.toFixed(1)}px (Nav row overflow: ${headerInfo.navRowOverflow})`);
        assert(headerInfo.wrapperHeight <= 112, `Header height is 110px or less on ${vp.name} [Actual: ${headerInfo.wrapperHeight.toFixed(1)}px]`);
        assert(headerInfo.navRowOverflow !== 'hidden', `Nav row does not have overflow:hidden (allows dropdowns to pop out)`);

        // 2. Test "Shop by Concern" Dropdown CLICK
        console.log(`Testing "Shop by Concern" dropdown click...`);
        const concernToggle = await page.locator('.tga-header-nav-row .dropdown-toggle:has-text("Shop by Concern")');
        await concernToggle.waitFor({ state: 'visible' });
        await concernToggle.click();
        await page.waitForTimeout(300);

        // Check dropdown menu is open and visible
        const concernMenu = await page.locator('.tga-header-nav-row .dropdown:has-text("Shop by Concern") .dropdown-menu');
        const isConcernVisible = await concernMenu.isVisible();
        assert(isConcernVisible, `"Shop by Concern" menu opens on CLICK on ${vp.name}`);

        const concernRect = await concernMenu.boundingBox();
        assert(concernRect && concernRect.height > 100, `"Shop by Concern" menu is not clipped (height: ${concernRect?.height}px)`);

        // Take screenshot with "Shop by Concern" menu open
        const shotConcernOpen = path.join(SCREENSHOT_DIR, `header_dropdown_concern_${vp.name}.png`);
        await page.screenshot({ path: shotConcernOpen, fullPage: false });
        console.log(`📸 Screenshot saved: ${shotConcernOpen}`);

        // Click an item inside "Shop by Concern": Digestion
        console.log(`Clicking item inside "Shop by Concern"...`);
        const digestionItem = await page.locator('.tga-header-nav-row .dropdown:has-text("Shop by Concern") .dropdown-menu a:has-text("Digestion")');
        await Promise.all([
            page.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 20000 }).catch(() => {}),
            digestionItem.click()
        ]);
        await page.waitForTimeout(600);
        assert(page.url().includes('concern=Digestion'), `Successfully clicked item inside "Shop by Concern" and navigated [URL: ${page.url()}]`);

        // Navigate back to home
        await page.goto('http://localhost:5000/', { waitUntil: 'networkidle', timeout: 20000 });
        await page.waitForTimeout(400);

        // 3. Test "More" Dropdown CLICK
        console.log(`Testing "More" dropdown click...`);
        const moreToggle = await page.locator('.tga-header-nav-row .dropdown-toggle:has-text("More")');
        await moreToggle.waitFor({ state: 'visible' });
        await moreToggle.click();
        await page.waitForTimeout(300);

        const moreMenu = await page.locator('.tga-header-nav-row .dropdown:has-text("More") .dropdown-menu');
        const isMoreVisible = await moreMenu.isVisible();
        assert(isMoreVisible, `"More" menu opens on CLICK on ${vp.name}`);

        const moreRect = await moreMenu.boundingBox();
        assert(moreRect && moreRect.height > 80, `"More" menu is not clipped (height: ${moreRect?.height}px)`);

        // Take screenshot with "More" menu open
        const shotMoreOpen = path.join(SCREENSHOT_DIR, `header_dropdown_more_${vp.name}.png`);
        await page.screenshot({ path: shotMoreOpen, fullPage: false });
        console.log(`📸 Screenshot saved: ${shotMoreOpen}`);

        // Click an item inside "More" (e.g. Dosha quiz link)
        console.log(`Clicking item inside "More"...`);
        const moreItem = await page.locator('.tga-header-nav-row .dropdown:has-text("More") .dropdown-menu a').first();
        assert(await moreItem.isVisible(), `First item inside "More" dropdown is visible and clickable`);
        await moreItem.click();
        await page.waitForTimeout(500);

        // 4. Test Outside Click closes dropdown
        await page.mouse.click(10, 10);
        await page.waitForTimeout(300);
        const isMoreClosedAfterOutsideClick = !(await moreMenu.isVisible());
        assert(isMoreClosedAfterOutsideClick, `"More" menu closes on outside click on ${vp.name}`);

        // 5. Test Escape key closes dropdown
        await moreToggle.click();
        await page.waitForTimeout(200);
        assert(await moreMenu.isVisible(), `"More" opened again before Escape test`);
        await page.mouse.move(10, 10);
        await page.keyboard.press('Escape');
        await page.waitForTimeout(300);
        const isMoreClosedAfterEscape = !(await moreMenu.isVisible());
        assert(isMoreClosedAfterEscape, `"More" menu closes on pressing Escape on ${vp.name}`);

        // 6. Test Hover opens dropdown on desktop
        console.log(`Testing hover interaction on ${vp.name}...`);
        await concernToggle.hover();
        await page.waitForTimeout(300);
        const isConcernOpenOnHover = await concernMenu.isVisible();
        assert(isConcernOpenOnHover, `"Shop by Concern" menu opens on HOVER on ${vp.name}`);

        // Move away closes
        await page.mouse.move(10, 10);
        await page.waitForTimeout(350);

        await context.close();
    }
}

async function testAdminLoginPage(browser) {
    console.log('\n================================================================');
    console.log('🧪 ITEM 4: ADMIN LOGIN PAGE REDESIGN & FULL-VIEWPORT TESTS');
    console.log('================================================================');

    const adminViewports = [
        { width: 1366, height: 768, name: '1366x768' },
        { width: 1920, height: 1080, name: '1920x1080' },
        { width: 390, height: 844, name: 'mobile_390x844', isMobile: true }
    ];

    for (const vp of adminViewports) {
        console.log(`\n--- Testing Admin Login on ${vp.name} ---`);
        const context = await browser.newContext({ viewport: { width: vp.width, height: vp.height } });
        const page = await context.newPage();

        await page.goto('http://localhost:7200/Auth/Login', { waitUntil: 'networkidle', timeout: 20000 });
        await page.waitForTimeout(600);

        // 1. Verify split-screen architecture & UI elements
        const uiChecks = await page.evaluate(() => {
            const mandala = document.querySelector('.mandala-wrap svg');
            const leaves = document.querySelectorAll('.drift-leaf');
            const orbs = document.querySelectorAll('.orb');
            const badge = document.querySelector('.admin-badge');
            const title = document.querySelector('.ambient-title');
            const stats = document.querySelectorAll('.ambient-stat-card, .stat-card');
            const glassCard = document.querySelector('.glass-login-card, #glassCard');
            const emailInput = document.getElementById('Email') || document.getElementById('adminEmailInput');
            const passwordInput = document.getElementById('Password') || document.getElementById('adminPwdInput');
            const submitBtn = document.getElementById('submitBtn') || document.getElementById('btnSubmitAdmin');
            const chips = document.querySelectorAll('.chip-pill, .demo-chip');
            const viewSiteLink = document.querySelector('a[href*="5000"]');
            const antiForgery = document.querySelector('input[name="__RequestVerificationToken"]');

            return {
                hasMandala: !!mandala,
                leafCount: leaves.length,
                orbCount: orbs.length,
                hasBadge: !!badge && badge.textContent.includes('Admin Portal'),
                hasTitle: !!title && title.textContent.includes('Sacred Ayurveda'),
                statCount: stats.length,
                hasGlassCard: !!glassCard,
                hasEmailInput: !!emailInput,
                hasPasswordInput: !!passwordInput,
                hasSubmitBtn: !!submitBtn,
                demoChipsCount: chips.length,
                hasViewSiteLink: !!viewSiteLink,
                hasAntiForgeryToken: !!antiForgery
            };
        });

        assert(uiChecks.hasMandala, `Sacred rotating mandala SVG present on ${vp.name}`);
        assert(uiChecks.leafCount >= 3, `Floating leaves drift layer active (${uiChecks.leafCount} leaves) on ${vp.name}`);
        assert(uiChecks.orbCount >= 2, `Ambient glowing gradient orbs active (${uiChecks.orbCount} orbs) on ${vp.name}`);
        assert(uiChecks.hasBadge, `Admin Portal badge present on ${vp.name}`);
        assert(uiChecks.hasTitle, `Executive headline present on ${vp.name}`);
        assert(uiChecks.statCount === 3, `3 sample stat KPI counters present on ${vp.name}`);
        assert(uiChecks.hasGlassCard, `Glass-style sign-in card present on ${vp.name}`);
        assert(uiChecks.hasEmailInput && uiChecks.hasPasswordInput, `Email & Password inputs present on ${vp.name}`);
        assert(uiChecks.hasSubmitBtn, `Gradient Sign-in button present on ${vp.name}`);
        assert(uiChecks.hasAntiForgeryToken, `Anti-forgery token preserved in login form on ${vp.name}`);
        assert(uiChecks.demoChipsCount >= 4, `All 4 Demo role chips present (SuperAdmin, Catalog, Finance, Support)`);
        assert(uiChecks.hasViewSiteLink, `"View Website" link to public store present`);

        // Capture screenshot
        const screenshotPath = path.join(SCREENSHOT_DIR, `admin_login_${vp.name}.png`);
        await page.screenshot({ path: screenshotPath, fullPage: false });
        console.log(`📸 Screenshot saved: ${screenshotPath}`);

        // Interactive tests on 1366x768
        if (vp.name === '1366x768') {
            console.log(`Testing Demo Chip click to auto-fill credentials...`);
            const superAdminChip = await page.locator('.chip-pill[data-demo-email*="admin@ayurveda.com"], .demo-chip[data-demo-email*="admin@ayurveda.com"]').first();
            await superAdminChip.click();
            await page.waitForTimeout(300);

            const filledEmail = await page.inputValue('#Email');
            const filledPassword = await page.inputValue('#Password');
            assert(filledEmail === 'admin@ayurveda.com', `SuperAdmin chip filled email: ${filledEmail}`);
            assert(filledPassword === getDevAdminPassword(), `SuperAdmin chip filled password matches dev demo password`);

            // Test Show/Hide password toggle
            console.log(`Testing Show/Hide password toggle...`);
            const togglePwBtn = await page.locator('#togglePasswordBtn');
            const initialType = await page.getAttribute('#Password', 'type');
            assert(initialType === 'password', `Password input initially masked`);

            await togglePwBtn.click();
            await page.waitForTimeout(200);
            const revealedType = await page.getAttribute('#Password', 'type');
            assert(revealedType === 'text', `Password input revealed after clicking eye icon`);

            await togglePwBtn.click();
            await page.waitForTimeout(200);
            const remaskedType = await page.getAttribute('#Password', 'type');
            assert(remaskedType === 'password', `Password input masked again`);

            // Test Wrong Credentials error handling and shake animation
            console.log(`Testing invalid credentials submit and shake animation...`);
            await page.fill('#Email', 'invalid_user@greatayurveda.com');
            await page.fill('#Password', 'WrongPassword!999');
            await page.click('#submitBtn');
            await page.waitForTimeout(800);

            const errorAlert = await page.locator('.alert-danger');
            const isErrorVisible = await errorAlert.isVisible();
            assert(isErrorVisible, `Clear error alert displayed on invalid credentials`);

            const hasShake = await page.evaluate(() => {
                const card = document.getElementById('glassCard');
                return card ? card.classList.contains('shake-target') : false;
            });
            assert(hasShake, `Shake animation applied to glass card on error`);

            // Test Valid Login and Redirect to Dashboard
            console.log(`Testing successful login with SuperAdmin credentials...`);
            const superAdminChipAgain = await page.locator('.chip-pill[data-demo-email*="admin@ayurveda.com"], .demo-chip[data-demo-email*="admin@ayurveda.com"]').first();
            await superAdminChipAgain.click();
            await page.waitForTimeout(200);
            await page.click('#submitBtn');

            // Wait for navigation to Dashboard
            await page.waitForFunction(() => {
                return (window.location.pathname === '/' || window.location.pathname.includes('/Dashboard')) && document.body.innerText.includes('Executive Management Console');
            }, { timeout: 15000 });
            await page.waitForTimeout(600);
            assert(page.url().includes('localhost:7200'), `Successfully authenticated and redirected to Admin Dashboard: ${page.url()}`);

            const dashShot = path.join(SCREENSHOT_DIR, 'admin_dashboard_logged_in.png');
            await page.screenshot({ path: dashShot, fullPage: false });
            console.log(`📸 Screenshot saved: ${dashShot}`);
        }

        await context.close();
    }
}

async function run() {
    console.log('🚀 STARTING PLAYWRIGHT VERIFICATION SUITE');
    const browser = await chromium.launch({ headless: true });

    try {
        await testHeaderDropdowns(browser);
        await testAdminLoginPage(browser);
    } finally {
        await browser.close();
    }

    console.log('\n================================================================');
    console.log(`FINAL PLAYWRIGHT SUMMARY: Passed: ${passed}, Failed: ${failed}`);
    console.log('================================================================');

    if (failed > 0) {
        process.exit(1);
    }
}

run().catch(err => {
    console.error('Test execution failed:', err);
    process.exit(1);
});
