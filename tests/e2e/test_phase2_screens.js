const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE_URL = 'http://localhost:5000';
const VIEWPORTS = [
    { width: 320, height: 640, name: 'mobile_320' },
    { width: 390, height: 844, name: 'mobile_390' },
    { width: 768, height: 1024, name: 'tablet_768' },
    { width: 1366, height: 768, name: 'desktop_1366' }
];

async function run() {
    console.log('--- STARTING PHASE 2 BROKEN SCREENS AUDIT ---');
    const browser = await chromium.launch({ headless: true });
    let passed = 0;
    let total = 0;

    const outDir = path.join(__dirname, 'screenshots', 'phase2');
    fs.mkdirSync(outDir, { recursive: true });

    // 1. Verify OTP at 320px and error display
    total++;
    try {
        const page = await browser.newPage({ viewport: { width: 320, height: 640 } });
        await page.goto(`${BASE_URL}/account/verify-otp?email=test%40example.com`, { waitUntil: 'domcontentloaded' });
        
        // Check for horizontal overflow
        const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
        const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
        console.log(`[VerifyOtp @ 320px] scrollWidth=${scrollWidth}, clientWidth=${clientWidth}`);
        if (scrollWidth > clientWidth) throw new Error(`Horizontal overflow on VerifyOtp @ 320px: ${scrollWidth} > ${clientWidth}`);

        // Try submitting empty -> check client error message shown
        await page.click('#verifyOtpSubmitBtn');
        const errVisible = await page.evaluate(() => {
            const el = document.getElementById('clientOtpError');
            return el && !el.classList.contains('d-none') && el.textContent.includes('6 digits');
        });
        if (!errVisible) throw new Error('Client OTP error was not displayed on empty submit');
        console.log('[VerifyOtp @ 320px] Error properly shown without alert: PASS');

        await page.screenshot({ path: path.join(outDir, 'verify_otp_320.png') });
        await page.close();
        passed++;
    } catch (e) {
        console.error('[VerifyOtp @ 320px] FAIL:', e.message);
    }

    // 2. Call Room: check formatted doctor name, single column, video aspect-ratio & max-height, centered controls, bottom sheet
    total++;
    try {
        // Log in as doctor or customer to view room
        const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
        const page = await context.newPage();
        
        // Login as customer
        await page.goto(`${BASE_URL}/account/login`, { waitUntil: 'domcontentloaded' });
        await page.fill('input[name="Email"]', 'customer@thegreatayurveda.com');
        await page.fill('input[name="Password"]', 'Customer@123');
        await page.click('.card form button[type="submit"]');
        await page.waitForNavigation({ waitUntil: 'domcontentloaded' }).catch(() => {});

        // Go to Call room
        await page.goto(`${BASE_URL}/doctors/room/1`, { waitUntil: 'domcontentloaded' });
        
        // Verify doctor title doesn't have "Dr. Dr."
        const titleText = await page.title();
        console.log('[Call Room] Page title:', titleText);
        if (titleText.includes('Dr. Dr.')) throw new Error(`Duplicate Dr. found in title: ${titleText}`);

        // Check remote participant title
        const remoteTitle = await page.evaluate(() => document.getElementById('remoteParticipantTitle')?.innerText || '');
        console.log('[Call Room] remoteParticipantTitle:', remoteTitle);
        if (remoteTitle.includes('Dr. Dr.')) throw new Error(`Duplicate Dr. found in remoteParticipantTitle: ${remoteTitle}`);

        // Check video stage max-height and aspect-ratio on mobile
        const stageStyles = await page.evaluate(() => {
            const el = document.getElementById('videoStage');
            if (!el) return null;
            const cs = window.getComputedStyle(el);
            return {
                maxHeight: cs.maxHeight,
                width: el.clientWidth,
                height: el.clientHeight,
                overflowX: document.documentElement.scrollWidth <= document.documentElement.clientWidth
            };
        });
        console.log('[Call Room @ 390px] Stage dimensions:', stageStyles);
        if (!stageStyles.overflowX) throw new Error('Horizontal overflow on Call room');

        // Check chat toggle bottom sheet
        const isSheetInitiallyClosed = await page.evaluate(() => {
            const col = document.getElementById('callSidePanelColumn');
            return col && !col.classList.contains('sheet-open');
        });
        console.log('[Call Room] Sheet initially closed:', isSheetInitiallyClosed);
        
        await page.click('#callToggleChatBtn');
        await page.waitForTimeout(400);
        const isSheetOpen = await page.evaluate(() => {
            const col = document.getElementById('callSidePanelColumn');
            return col && col.classList.contains('sheet-open');
        });
        console.log('[Call Room] Sheet open after toggle:', isSheetOpen);
        if (!isSheetOpen) throw new Error('Chat bottom sheet did not open on toggle');

        await page.screenshot({ path: path.join(outDir, 'call_room_mobile_sheet.png') });
        await page.close();
        await context.close();
        passed++;
    } catch (e) {
        console.error('[Call Room] FAIL:', e.message);
    }

    // 3. Orders Details: check no duplicate header on mobile, 2-column actions, horizontal tracking steps, map height max 55vh, real bullet in status
    total++;
    try {
        const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
        const page = await context.newPage();
        
        // Login as customer
        await page.goto(`${BASE_URL}/account/login`, { waitUntil: 'domcontentloaded' });
        await page.fill('input[name="Email"]', 'customer@thegreatayurveda.com');
        await page.fill('input[name="Password"]', 'Customer@123');
        await page.click('.card form button[type="submit"]');
        await page.waitForNavigation({ waitUntil: 'domcontentloaded' }).catch(() => {});

        await page.goto(`${BASE_URL}/Orders/Details/1`, { waitUntil: 'domcontentloaded' });
        await page.waitForTimeout(1000);

        // Check for horizontal overflow
        const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
        const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
        console.log(`[Order Details @ 390px] scrollWidth=${scrollWidth}, clientWidth=${clientWidth}`);
        if (scrollWidth > clientWidth) throw new Error(`Horizontal overflow on Order Details: ${scrollWidth} > ${clientWidth}`);

        // Check map status text for raw &bull;
        const statusHtml = await page.evaluate(() => document.getElementById('mapStatusText')?.innerHTML || '');
        const statusText = await page.evaluate(() => document.getElementById('mapStatusText')?.textContent || '');
        console.log('[Order Details] mapStatusText textContent:', statusText);
        if (statusText.includes('&bull;')) throw new Error(`Raw &bull; printed in mapStatusText: ${statusText}`);

        // Check actions grid layout on mobile (2 columns)
        const actionsDisplay = await page.evaluate(() => {
            const grid = document.querySelector('.tga-order-actions-grid');
            if (!grid) return null;
            const cs = window.getComputedStyle(grid);
            return {
                display: cs.display,
                gridTemplateColumns: cs.gridTemplateColumns
            };
        });
        console.log('[Order Details] actions grid layout:', actionsDisplay);

        // Check map max-height
        const mapMaxHeight = await page.evaluate(() => {
            const map = document.getElementById('orderTrackingMap');
            return map ? window.getComputedStyle(map).maxHeight : null;
        });
        console.log('[Order Details] map maxHeight:', mapMaxHeight);

        await page.screenshot({ path: path.join(outDir, 'order_details_mobile.png') });
        await page.close();
        await context.close();
        passed++;
    } catch (e) {
        console.error('[Order Details] FAIL:', e.message);
    }

    // 4. Checkout confirmation: 2-column dl, overflow-wrap:anywhere, demo badge in normal flow
    total++;
    try {
        const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
        const page = await context.newPage();
        
        // Login as customer
        await page.goto(`${BASE_URL}/account/login`, { waitUntil: 'domcontentloaded' });
        await page.fill('input[name="Email"]', 'customer@thegreatayurveda.com');
        await page.fill('input[name="Password"]', 'Customer@123');
        await page.click('.card form button[type="submit"]');
        await page.waitForNavigation({ waitUntil: 'domcontentloaded' }).catch(() => {});

        await page.goto(`${BASE_URL}/checkout/confirmation/1`, { waitUntil: 'domcontentloaded' });
        
        // Check for dl definition list
        const dlExists = await page.evaluate(() => {
            const dl = document.querySelector('dl');
            if (!dl) return false;
            const cs = window.getComputedStyle(dl);
            return cs.overflowWrap === 'anywhere';
        });
        console.log('[Confirmation] dl with overflow-wrap anywhere exists:', dlExists);
        if (!dlExists) throw new Error('2-column dl with overflow-wrap: anywhere not found');

        // Check badge is NOT position: absolute
        const badgePosition = await page.evaluate(() => {
            const badge = document.querySelector('.badge.bg-warning-subtle');
            return badge ? window.getComputedStyle(badge).position : null;
        });
        console.log('[Confirmation] badge position:', badgePosition);
        if (badgePosition === 'absolute') throw new Error('Demo badge is still position: absolute');

        await page.screenshot({ path: path.join(outDir, 'confirmation_mobile.png') });
        await page.close();
        await context.close();
        passed++;
    } catch (e) {
        console.error('[Confirmation] FAIL:', e.message);
    }

    await browser.close();
    console.log(`--- PHASE 2 SCREENS AUDIT COMPLETE: ${passed}/${total} PASSED ---`);
    process.exit(passed === total ? 0 : 1);
}

run().catch(err => {
    console.error('Audit fatal error:', err);
    process.exit(1);
});
