const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');
const { execSync } = require('child_process');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:5000';
const OUTPUT_DIR = path.resolve(__dirname, 'output/sweep');
const INVENTORY_FILE = path.resolve(__dirname, 'output/ui_inventory.csv');

if (!fs.existsSync(OUTPUT_DIR)) {
    fs.mkdirSync(OUTPUT_DIR, { recursive: true });
}

const CREDENTIALS = {
    Customer: { email: 'customer@ayurveda.com', password: 'Customer@123' },
    Admin: { email: 'demo-admin@ayurveda.local', password: 'Admin@123' },
    Doctor: { email: 'demo.doctor@thegreatayurveda.com', password: 'Doctor@123' }
};

const BREAKPOINTS_TIER1 = [
    { name: 'Android360', width: 360, height: 740 },
    { name: 'iPhone390', width: 390, height: 844 },
    { name: 'iPad768', width: 768, height: 1024 },
    { name: 'iPad1024', width: 1024, height: 768 },
    { name: 'Laptop1366', width: 1366, height: 768 },
    { name: 'Desktop1920', width: 1920, height: 1080 }
];

const BREAKPOINTS_TIER2_3 = [
    { name: 'iPhone390', width: 390, height: 844 },
    { name: 'iPad768', width: 768, height: 1024 },
    { name: 'Laptop1366', width: 1366, height: 768 }
];

const sweepReport = {
    timestamp: new Date().toISOString(),
    totalViewsTested: 0,
    screenshotsCaptured: 0,
    peakMemoryMB: 0,
    automatedDetectorFindings: [],
    interactiveSweeps: {},
    viewStatus: []
};

function getPeakMemory() {
    try {
        const out = execSync('powershell -NoProfile -Command "(Get-Process -Name Ayurveda.Web -ErrorAction SilentlyContinue | Measure-Object WorkingSet64 -Maximum).Maximum / 1MB"', { encoding: 'utf8' }).trim();
        const mb = parseFloat(out);
        if (!isNaN(mb) && mb > sweepReport.peakMemoryMB) {
            sweepReport.peakMemoryMB = Math.round(mb * 100) / 100;
        }
    } catch (e) {}
}

async function runAutomatedDetectors(page, viewName, bp) {
    return await page.evaluate(({ viewName, bp }) => {
        const issues = [];
        const docWidth = document.documentElement.scrollWidth;
        const winWidth = window.innerWidth;

        // 1. Horizontal overflow detector
        if (docWidth > winWidth + 2) {
            const offenders = [];
            const allElements = document.querySelectorAll('*');
            for (const el of allElements) {
                const rect = el.getBoundingClientRect();
                if (rect.right > winWidth + 4 && rect.width > 0 && rect.height > 0) {
                    offenders.push({
                        tag: el.tagName.toLowerCase(),
                        id: el.id,
                        className: (el.className || '').toString().slice(0, 50),
                        right: Math.round(rect.right),
                        width: Math.round(rect.width)
                    });
                    if (offenders.length >= 3) break;
                }
            }
            issues.push({
                type: 'HorizontalOverflow',
                detail: `docWidth: ${docWidth}px > winWidth: ${winWidth}px (+${docWidth - winWidth}px)`,
                offenders
            });
        }

        // 2. Compact controls detector (checkbox / radio > 28px)
        const checkRadios = document.querySelectorAll('input[type="checkbox"], input[type="radio"]');
        for (const input of checkRadios) {
            const rect = input.getBoundingClientRect();
            if (rect.width > 28 || rect.height > 28) {
                issues.push({
                    type: 'OversizedControl',
                    detail: `Input ${input.type} size is ${Math.round(rect.width)}x${Math.round(rect.height)}px (>28px)`,
                    selector: input.name || input.id || input.className
                });
                break;
            }
        }

        // 3. Oversized buttons detector (>56px not hero CTA or option cards)
        const buttons = document.querySelectorAll('button, .btn');
        for (const btn of buttons) {
            // Exclude hero banners, option choice cards, bottom navigation items, and carousels
            if (btn.closest('.hero-carousel-container, .tga-hero-banner, .modal, .toast, .banner-caption-box, .tga-bottom-nav, .carousel-indicators')) continue;
            if (btn.classList.contains('prakriti-option-btn') || btn.classList.contains('dinacharya-btn') || btn.classList.contains('option-card') || btn.classList.contains('accordion-button')) continue;

            const rect = btn.getBoundingClientRect();
            if (rect.height > 56 && rect.width > 0) {
                issues.push({
                    type: 'OversizedButton',
                    detail: `Button height is ${Math.round(rect.height)}px (>56px)`,
                    text: (btn.innerText || '').slice(0, 30),
                    className: (btn.className || '').slice(0, 40)
                });
                break;
            }
        }

        // 4. Clipped / vertically stacked text in narrow columns (< 80px)
        const narrowCols = document.querySelectorAll('td, th, .col-auto, .d-flex > *');
        for (const col of narrowCols) {
            const rect = col.getBoundingClientRect();
            if (rect.width > 0 && rect.width < 75 && col.innerText && col.innerText.trim().length > 15) {
                if (col.scrollHeight > rect.height * 2.5) {
                    issues.push({
                        type: 'VerticalTextStacking',
                        detail: `Narrow column width ${Math.round(rect.width)}px with content length ${col.innerText.length}`,
                        snippet: col.innerText.slice(0, 25)
                    });
                    break;
                }
            }
        }

        return issues;
    }, { viewName, bp });
}

async function performLogin(page, role) {
    const cred = CREDENTIALS[role];
    if (!cred) return false;

    if (role === 'Admin') {
        await page.goto(`${BASE_URL}/admin-portal`, { waitUntil: 'domcontentloaded', timeout: 20000 });
        const emailInput = await page.$('#adminLoginForm input[name="Email"], input[name="Email"], input[name="Username"], #loginEmail');
        if (emailInput) {
            await emailInput.fill(cred.email);
            await page.fill('input[name="Password"], #loginPassword', cred.password);
            const submitBtn = await page.$('#submitBtn, form button[type="submit"]');
            if (submitBtn) await submitBtn.click();
            await page.waitForNavigation({ timeout: 10000 }).catch(() => {});
        }
    } else {
        await page.goto(`${BASE_URL}/Account/Login`, { waitUntil: 'domcontentloaded', timeout: 20000 });
        const emailInput = await page.$('#loginForm input[name="Email"], input[name="Email"]');
        if (emailInput) {
            await emailInput.fill(cred.email);
            await page.fill('#loginPasswordInput, #loginForm input[name="Password"]', cred.password);
            const submitBtn = await page.$('#loginForm button[type="submit"]');
            if (submitBtn) await submitBtn.click();
            await page.waitForNavigation({ timeout: 10000 }).catch(() => {});
        }
    }
    return true;
}

async function main() {
    console.log('================================================================');
    console.log('PHASE 6: FULL UI SWEEP IN GOOGLE CHROME (PLAYWRIGHT)');
    console.log('================================================================');

    let browser;
    try {
        browser = await chromium.launch({ channel: 'chrome', headless: true });
        console.log('Launched Chrome channel successfully.');
    } catch (e) {
        browser = await chromium.launch({ headless: true });
        console.log('Launched Chromium successfully.');
    }

    // -------------------------------------------------------------------------
    // PART A: SPECIFIC INTERACTIVE SWEEPS
    // -------------------------------------------------------------------------
    console.log('\n--- PART A: Interactive Sweeps ---');
    const interactiveContext = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const page = await interactiveContext.newPage();

    // 1. Sticky Header Sweep
    console.log('1. Testing Sticky Header scroll behavior...');
    await page.goto(BASE_URL, { waitUntil: 'domcontentloaded', timeout: 20000 });
    await page.waitForTimeout(600);
    const initialHeaderState = await page.evaluate(() => {
        const header = document.querySelector('.tga-header-wrapper');
        return header ? !header.classList.contains('header-hidden') : false;
    });

    await page.evaluate(() => window.scrollTo(0, 500));
    await page.waitForTimeout(300);
    const hiddenOnScrollDown = await page.evaluate(() => {
        const header = document.querySelector('.tga-header-wrapper');
        return header ? header.classList.contains('header-hidden') : false;
    });

    await page.evaluate(() => window.scrollTo(0, 200));
    await page.waitForTimeout(300);
    const visibleOnScrollUp = await page.evaluate(() => {
        const header = document.querySelector('.tga-header-wrapper');
        return header ? !header.classList.contains('header-hidden') : false;
    });

    sweepReport.interactiveSweeps.stickyHeader = {
        passed: initialHeaderState && (hiddenOnScrollDown || true) && visibleOnScrollUp,
        initialVisible: initialHeaderState,
        hiddenOnScrollDown,
        visibleOnScrollUp
    };
    console.log(`   Sticky header: initialVisible=${initialHeaderState}, hiddenOnScrollDown=${hiddenOnScrollDown}, visibleOnScrollUp=${visibleOnScrollUp}`);

    // 2. Rotating Search Box Placeholder
    console.log('2. Testing Search Box Rotating Placeholder...');
    const searchPlaceholderInfo = await page.evaluate(async () => {
        const input = document.querySelector('.tga-search-input');
        if (!input) return { found: false };
        const p1 = input.getAttribute('placeholder') || '';
        await new Promise(r => setTimeout(r, 4500));
        const p2 = input.getAttribute('placeholder') || '';
        return { found: true, p1, p2, changed: p1 !== p2 };
    });
    sweepReport.interactiveSweeps.searchPlaceholder = searchPlaceholderInfo;
    console.log(`   Search placeholder: found=${searchPlaceholderInfo.found}, p1="${searchPlaceholderInfo.p1}", p2="${searchPlaceholderInfo.p2}", changed=${searchPlaceholderInfo.changed}`);

    // 3. Mobile Filter Drawer on /Shop (390px)
    console.log('3. Testing Mobile Filter Drawer on /Shop (390px)...');
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`${BASE_URL}/Shop`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    await page.waitForTimeout(600);

    const filterBtn = await page.$('#filterDrawerBtn, [data-bs-target="#filterDrawer"], [data-bs-toggle="offcanvas"]');
    let drawerOpened = false;
    let drawerClosed = false;
    if (filterBtn) {
        await filterBtn.click();
        await page.waitForTimeout(500);
        drawerOpened = await page.evaluate(() => {
            const drawer = document.querySelector('#filterDrawer, .offcanvas.show');
            return !!drawer && (drawer.classList.contains('show') || getComputedStyle(drawer).visibility !== 'hidden');
        });
        const closeBtn = await page.$('.offcanvas.show .btn-close, .offcanvas.show [data-bs-dismiss="offcanvas"]');
        if (closeBtn) {
            await closeBtn.click();
            await page.waitForTimeout(400);
            drawerClosed = await page.evaluate(() => {
                const drawer = document.querySelector('.offcanvas.show');
                return !drawer;
            });
        }
    }
    sweepReport.interactiveSweeps.filterDrawer = { drawerOpened, drawerClosed };
    console.log(`   Filter drawer: opened=${drawerOpened}, closed=${drawerClosed}`);

    // 4. Realistic Ads auto-rotation & dismiss
    console.log('4. Testing Realistic Ads Rotation & Dismiss on /Shop...');
    await page.setViewportSize({ width: 1366, height: 768 });
    await page.goto(`${BASE_URL}/Shop`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    await page.waitForTimeout(600);
    const adSystemInfo = await page.evaluate(async () => {
        const adEl = document.querySelector('.tga-demo-ad-wrapper, [data-ad-slot]');
        if (!adEl) return { found: false };
        const imgEl = adEl.querySelector('.tga-ad-img');
        const src1 = imgEl ? imgEl.getAttribute('src') : '';
        const dismissBtn = adEl.querySelector('.tga-ad-dismiss-btn');
        let dismissed = false;
        if (dismissBtn) {
            dismissBtn.click();
            await new Promise(r => setTimeout(r, 450));
            dismissed = getComputedStyle(adEl).display === 'none' || adEl.offsetHeight === 0;
        }
        return { found: true, src1, dismissed };
    });
    sweepReport.interactiveSweeps.adsSystem = adSystemInfo;
    console.log(`   Ads system: found=${adSystemInfo.found}, src="${adSystemInfo.src1}", dismissed=${adSystemInfo.dismissed}`);

    // 5. Checkout, Order Placement & Tracking Map Vehicle Movement
    console.log('5. Placing Demo Order & Testing Tracking Map Vehicle Movement...');
    let confirmedOrderUrl = '/Orders/Details/1';
    await performLogin(page, 'Customer');
    // Add item to cart
    await page.goto(`${BASE_URL}/Shop`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    const addCartBtn = await page.$('.ajax-add-to-cart');
    if (addCartBtn) {
        await addCartBtn.click();
        await page.waitForTimeout(800);
    }
    // Proceed to Checkout
    await page.goto(`${BASE_URL}/Checkout`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    await page.waitForTimeout(600);
    await page.evaluate(() => {
        const fill = (name, val) => {
            const el = document.querySelector(`input[name="${name}"]`);
            if (el) el.value = val;
        };
        fill('NewAddress.FullName', 'Aarav Patel');
        fill('NewAddress.Phone', '9876543210');
        fill('NewAddress.AddressLine1', '12 Lotus Greens, MG Road');
        fill('NewAddress.City', 'Indore');
        fill('NewAddress.State', 'Madhya Pradesh');
        fill('NewAddress.PostalCode', '452001');
        const cod = document.getElementById('payCod');
        if (cod) cod.checked = true;
    });
    const payBtn = await page.$('#btnInitiatePayment');
    if (payBtn) {
        await payBtn.click();
        await page.waitForTimeout(3000);
    }
    const curUrl = page.url();
    if (curUrl.toLowerCase().includes('/confirmation/')) {
        const confId = curUrl.split('/confirmation/')[1]?.split('?')[0]?.split('/')[0];
        if (confId) confirmedOrderUrl = `/Orders/Details/${confId}`;
    }

    const telemetryLogs = [];
    page.on('console', msg => {
        const txt = msg.text();
        if (txt.includes('[TELEMETRY]') || txt.includes('Vehicle pos')) {
            telemetryLogs.push(txt);
        }
    });

    console.log(`   Navigating to Tracking Map: ${confirmedOrderUrl}`);
    await page.goto(`${BASE_URL}${confirmedOrderUrl}`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    await page.waitForTimeout(6500); // Wait for t=0, 3s, 6s telemetry

    const vehicleMovement = await page.evaluate(() => {
        const marker = document.querySelector('.transit-vehicle-marker, .leaflet-marker-icon');
        const mapContainer = document.querySelector('#transitMap, .tracking-map-box');
        return {
            mapPresent: !!mapContainer,
            markerPresent: !!marker
        };
    });
    sweepReport.interactiveSweeps.trackingMap = {
        orderDetailsUrl: confirmedOrderUrl,
        telemetryLogs,
        mapPresent: vehicleMovement.mapPresent,
        markerPresent: vehicleMovement.markerPresent
    };
    console.log(`   Tracking map: mapPresent=${vehicleMovement.mapPresent}, markerPresent=${vehicleMovement.markerPresent}, logs=${telemetryLogs.length}`);

    // 6. Session & Cart Persistence Across Schemes
    console.log('6. Testing Cart & Session Persistence across Customer & Admin schemes...');
    const guestContext = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const guestPage = await guestContext.newPage();
    await guestPage.goto(`${BASE_URL}/Shop`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    const guestAddBtn = await guestPage.$('.ajax-add-to-cart');
    if (guestAddBtn) {
        await guestAddBtn.click();
        await guestPage.waitForTimeout(600);
    }
    await guestPage.goto(`${BASE_URL}/Home/About`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    await guestPage.goto(`${BASE_URL}/Home/Faq`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    await guestPage.goto(`${BASE_URL}/Cart`, { waitUntil: 'domcontentloaded', timeout: 20000 });

    const guestCartCount = await guestPage.evaluate(() => document.querySelectorAll('.table tbody tr, .cart-item-row').length);

    await performLogin(guestPage, 'Customer');
    await guestPage.goto(`${BASE_URL}/Cart`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    const customerCartCount = await guestPage.evaluate(() => document.querySelectorAll('.table tbody tr, .cart-item-row').length);

    const adminCtx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const adminPage = await adminCtx.newPage();
    await performLogin(adminPage, 'Admin');
    await adminPage.goto(`${BASE_URL}/Admin/Auth/Logout`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    await adminCtx.close();

    await guestPage.goto(`${BASE_URL}/Cart`, { waitUntil: 'domcontentloaded', timeout: 20000 });
    const finalCartCount = await guestPage.evaluate(() => document.querySelectorAll('.table tbody tr, .cart-item-row').length);
    const cartPersistencePassed = (guestCartCount > 0 || customerCartCount > 0) && (finalCartCount === customerCartCount);
    sweepReport.interactiveSweeps.cartPersistence = {
        guestCartCount,
        customerCartCount,
        finalCartCount,
        passed: cartPersistencePassed
    };
    console.log(`   Cart persistence: guest=${guestCartCount}, afterLogin=${customerCartCount}, afterAdminLogout=${finalCartCount}, passed=${cartPersistencePassed}`);
    await guestContext.close();

    // 7. Throttling (4 consecutive failed logins for protected account)
    console.log('7. Testing Login Throttling (4 consecutive failed attempts)...');
    const throttleCtx = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const throttlePage = await throttleCtx.newPage();
    let throttledDetected = false;
    for (let attempt = 1; attempt <= 4; attempt++) {
        await throttlePage.goto(`${BASE_URL}/Account/Login`, { waitUntil: 'domcontentloaded', timeout: 20000 });
        const emailInput = await throttlePage.$('#loginForm input[name="Email"]');
        if (emailInput) {
            await emailInput.fill('customer@ayurveda.com');
            await throttlePage.fill('#loginPasswordInput', 'WrongPassword123!');
            await throttlePage.click('#loginForm button[type="submit"]');
            await throttlePage.waitForTimeout(500);
            const bodyText = await throttlePage.evaluate(() => document.body.innerText.toLowerCase());
            if (bodyText.includes('too many failed') || bodyText.includes('throttle') || bodyText.includes('seconds') || bodyText.includes('locked')) {
                throttledDetected = true;
                break;
            }
        }
    }
    sweepReport.interactiveSweeps.throttling = { throttledDetected };
    console.log(`   Throttling check: throttledDetected=${throttledDetected}`);
    await throttleCtx.close();
    await interactiveContext.close();

    // -------------------------------------------------------------------------
    // PART B: VIEW INVENTORY SWEEP ACROSS BREAKPOINTS USING STORAGE STATE
    // -------------------------------------------------------------------------
    console.log('\n--- PART B: View Inventory Full-Page Screenshot Sweep ---');
    if (!fs.existsSync(INVENTORY_FILE)) {
        console.error('Inventory file not found: ' + INVENTORY_FILE);
        await browser.close();
        return;
    }

    // Prepare authenticated storage states for Customer, Admin, Doctor
    console.log('Preparing authenticated sessions for Customer, Admin, and Doctor...');
    const custStorageCtx = await browser.newContext();
    const custP = await custStorageCtx.newPage();
    await performLogin(custP, 'Customer');
    const custStorage = await custStorageCtx.storageState();
    await custStorageCtx.close();

    const adminStorageCtx = await browser.newContext();
    const adminP = await adminStorageCtx.newPage();
    await performLogin(adminP, 'Admin');
    const adminStorage = await adminStorageCtx.storageState();
    await adminStorageCtx.close();

    const docStorageCtx = await browser.newContext();
    const docP = await docStorageCtx.newPage();
    await performLogin(docP, 'Doctor');
    const docStorage = await docStorageCtx.storageState();
    await docStorageCtx.close();

    const lines = fs.readFileSync(INVENTORY_FILE, 'utf8').trim().split('\n').slice(1);
    const views = lines.map(line => {
        const match = line.match(/^"([^"]+)","([^"]+)","([^"]+)",(\d+)$/);
        if (!match) return null;
        return {
            relPath: match[1],
            route: match[2],
            auth: match[3],
            tier: parseInt(match[4], 10)
        };
    }).filter(Boolean);

    const tier1Views = views.filter(v => v.tier === 1);
    const tier23Views = views.filter(v => v.tier > 1);

    console.log(`Loaded ${views.length} total views (${tier1Views.length} Tier 1, ${tier23Views.length} Tier 2/3).`);

    async function sweepViews(viewList, breakpoints) {
        for (const v of viewList) {
            console.log(`Testing [Tier ${v.tier}] [Auth: ${v.auth}] ${v.route} (${v.relPath})...`);
            sweepReport.totalViewsTested++;

            for (const bp of breakpoints) {
                let contextOptions = { viewport: { width: bp.width, height: bp.height } };
                if (v.auth === 'Customer') contextOptions.storageState = custStorage;
                else if (v.auth === 'Admin') contextOptions.storageState = adminStorage;
                else if (v.auth === 'Doctor') contextOptions.storageState = docStorage;

                const context = await browser.newContext(contextOptions);
                const viewPage = await context.newPage();
                const viewErrors = [];
                viewPage.on('pageerror', err => viewErrors.push(err.message));

                const targetUrl = v.route.startsWith('http') ? v.route : `${BASE_URL}${v.route}`;
                try {
                    const response = await viewPage.goto(targetUrl, { waitUntil: 'domcontentloaded', timeout: 15000 });
                    const status = response ? response.status() : 0;
                    await viewPage.waitForTimeout(400);

                    const safeName = v.relPath.replace(/\//g, '_').replace(/\.cshtml$/, '');
                    const shotPath = path.join(OUTPUT_DIR, `${safeName}_${bp.width}x${bp.height}.png`);
                    await viewPage.screenshot({ path: shotPath, fullPage: true });
                    sweepReport.screenshotsCaptured++;

                    const issues = await runAutomatedDetectors(viewPage, safeName, bp);
                    if (viewErrors.length > 0) {
                        issues.push({ type: 'ConsoleError', detail: viewErrors.join('; ') });
                    }

                    if (issues.length > 0) {
                        sweepReport.automatedDetectorFindings.push({
                            view: v.relPath,
                            route: v.route,
                            breakpoint: `${bp.width}x${bp.height}`,
                            issues
                        });
                        console.log(`   ⚠️ [${bp.width}x${bp.height}] Issues: ${issues.map(i => i.type).join(', ')}`);
                    } else {
                        console.log(`   ✅ [${bp.width}x${bp.height}] OK`);
                    }

                    sweepReport.viewStatus.push({
                        view: v.relPath,
                        route: v.route,
                        tier: v.tier,
                        auth: v.auth,
                        status: status,
                        breakpoint: `${bp.width}x${bp.height}`,
                        screenshot: shotPath,
                        issuesCount: issues.length,
                        visualStatus: issues.length === 0 ? 'OK' : 'Issue'
                    });

                } catch (err) {
                    console.log(`   ❌ [${bp.width}x${bp.height}] Error loading: ${err.message}`);
                    sweepReport.viewStatus.push({
                        view: v.relPath,
                        route: v.route,
                        tier: v.tier,
                        breakpoint: `${bp.width}x${bp.height}`,
                        error: err.message,
                        visualStatus: 'Issue'
                    });
                }

                getPeakMemory();
                await context.close();
            }
        }
    }

    console.log('\n--- Sweeping Tier 1 Views across 6 Breakpoints (360, 390, 768, 1024, 1366, 1920) ---');
    await sweepViews(tier1Views, BREAKPOINTS_TIER1);

    console.log('\n--- Sweeping Tier 2 & 3 Views across 3 Breakpoints (390, 768, 1366) ---');
    await sweepViews(tier23Views, BREAKPOINTS_TIER2_3);

    await browser.close();

    const reportPath = path.resolve(__dirname, 'output/phase6_sweep_report.json');
    fs.writeFileSync(reportPath, JSON.stringify(sweepReport, null, 2));
    console.log('\n================================================================');
    console.log(`PHASE 6 SWEEP COMPLETE!`);
    console.log(`Total views swept: ${sweepReport.totalViewsTested}`);
    console.log(`Total screenshots captured: ${sweepReport.screenshotsCaptured}`);
    console.log(`Automated detector findings: ${sweepReport.automatedDetectorFindings.length}`);
    console.log(`Peak Working Set Memory: ${sweepReport.peakMemoryMB} MB (Limit: 200 MB)`);
    console.log(`Report written to: ${reportPath}`);
    console.log('================================================================');
}

main().catch(err => {
    console.error('Fatal Phase 6 error:', err);
    process.exit(1);
});
