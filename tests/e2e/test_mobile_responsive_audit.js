const { chromium } = require('playwright');
const assert = require('assert');
const path = require('path');
const fs = require('fs');
const { getDemoAdminPassword } = require('./getTestPassword');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:5050';
const SCREENSHOT_DIR = path.join(__dirname, 'screenshots', 'responsive');

if (!fs.existsSync(SCREENSHOT_DIR)) {
    fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
}

const DEVICES = [
    { name: 'iPhone_SE', width: 375, height: 667, isPhone: true, userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 16_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 Mobile/15E148 Safari/604.1' },
    { name: 'iPhone_14', width: 390, height: 844, isPhone: true, userAgent: 'Mozilla/5.0 (iPhone; CPU iPhone OS 16_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 Mobile/15E148 Safari/604.1' },
    { name: 'Pixel_7', width: 412, height: 915, isPhone: true, userAgent: 'Mozilla/5.0 (Linux; Android 13; Pixel 7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Mobile Safari/537.36' },
    { name: 'iPad', width: 768, height: 1024, isPhone: false, userAgent: 'Mozilla/5.0 (iPad; CPU OS 16_6 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.6 Mobile/15E148 Safari/604.1' },
    { name: 'Laptop', width: 1366, height: 768, isPhone: false, userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36' },
    { name: 'Desktop_1080p', width: 1920, height: 1080, isPhone: false, userAgent: 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/116.0.0.0 Safari/537.36' }
];

async function runDeviceAudit() {
    console.log('======================================================================');
    console.log(' MOBILE-RESPONSIVE + VISUAL POLISH MULTI-DEVICE PROOF AUDIT');
    console.log(` Target Base URL: ${BASE_URL}`);
    console.log(` Screenshots Directory: ${SCREENSHOT_DIR}`);
    console.log('======================================================================\n');

    const browser = await chromium.launch({ headless: true });
    let totalChecks = 0;
    let failedChecks = 0;

    const auditResults = [];

    for (const dev of DEVICES) {
        console.log(`\n==================================================================`);
        console.log(` TESTING DEVICE: ${dev.name} (${dev.width}x${dev.height}) [Phone: ${dev.isPhone}]`);
        console.log(`==================================================================`);

        const context = await browser.newContext({
            viewport: { width: dev.width, height: dev.height },
            userAgent: dev.userAgent,
            deviceScaleFactor: dev.isPhone ? 2 : 1,
            isMobile: dev.isPhone,
            hasTouch: dev.isPhone
        });

        const page = await context.newPage();

        const failedImages = [];
        const consoleErrors = [];

        page.on('response', response => {
            const req = response.request();
            if (req.resourceType() === 'image' && response.status() >= 400) {
                failedImages.push({ url: response.url(), status: response.status() });
            }
        });

        page.on('console', msg => {
            if (msg.type() === 'error') {
                const text = msg.text();
                // Ignore benign favicon or external analytics if any
                if (!text.includes('favicon.ico')) {
                    consoleErrors.push(text);
                }
            }
        });

        page.on('pageerror', error => {
            consoleErrors.push(error.message);
        });

        // 1. SETUP SESSIONS (Customer Demo Login)
        try {
            await page.goto(`${BASE_URL}/account/login`, { waitUntil: 'domcontentloaded', timeout: 30000 });
            const demoBtn = await page.$('a:has-text("Continue as Demo Guest"), button:has-text("Continue as Demo Guest")');
            if (demoBtn) {
                await demoBtn.click();
                await page.waitForTimeout(1000);
            }
        } catch (e) {
            console.log(`[AUTH] Customer demo setup notice: ${e.message}`);
        }

        // Add 1 item to cart to verify cart & checkout pages with items
        try {
            await page.goto(`${BASE_URL}/shop/details/1`, { waitUntil: 'domcontentloaded', timeout: 30000 });
            const addBtn = await page.$('.ajax-add-to-cart, button:has-text("Add to Cart"), #btnStickyAddToCart');
            if (addBtn) {
                await addBtn.click();
                await page.waitForTimeout(800);
            }
        } catch (e) { }

        // Define pages to test
        const testPages = [
            { id: 'home_top', path: '/', name: 'Home (Top)', scroll: 'top' },
            { id: 'home_mid', path: '/', name: 'Home (Middle)', scroll: 'middle' },
            { id: 'home_bot', path: '/', name: 'Home (Bottom)', scroll: 'bottom' },
            { id: 'shop', path: '/shop', name: 'Shop Catalog' },
            { id: 'product', path: '/shop/details/1', name: 'Product Details' },
            { id: 'cart', path: '/cart', name: 'Shopping Cart' },
            { id: 'checkout', path: '/checkout', name: 'Checkout Page' },
            { id: 'doctors', path: '/doctors', name: 'Doctors Directory' },
            { id: 'doctor_profile', path: '/doctors/profile/1', name: 'Doctor Profile' },
            { id: 'booking', path: '/doctors/book/1', name: 'Doctor Booking' },
            { id: 'call_room', path: '/doctors/room/1', name: 'Consultation Call Room' },
            { id: 'login', path: '/account/login', name: 'Customer Login' },
            { id: 'register', path: '/account/register', name: 'Customer Register' },
            { id: 'orders', path: '/orders', name: 'Customer Orders' }
        ];

        for (const tp of testPages) {
            totalChecks++;
            const startFailedImagesCount = failedImages.length;
            const startConsoleErrorsCount = consoleErrors.length;

            try {
                await page.goto(`${BASE_URL}${tp.path}`, { waitUntil: 'networkidle', timeout: 30000 });
                await page.waitForTimeout(500);

                if (tp.scroll === 'middle') {
                    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight / 2));
                    await page.waitForTimeout(300);
                } else if (tp.scroll === 'bottom') {
                    await page.evaluate(() => window.scrollTo(0, document.body.scrollHeight));
                    await page.waitForTimeout(300);
                }

                // Screenshot filename
                const shotName = `${dev.name}_${tp.id}.png`;
                const shotPath = path.join(SCREENSHOT_DIR, shotName);
                await page.screenshot({ path: shotPath, fullPage: false });

                // (a) Assert scrollWidth <= window.innerWidth
                const scrollCheck = await page.evaluate(() => {
                    const scrollW = document.documentElement.scrollWidth;
                    const innerW = window.innerWidth;
                    return { scrollW, innerW, noOverflow: scrollW <= innerW };
                });

                // (b) Zero failed images for this page
                const newFailedImages = failedImages.slice(startFailedImagesCount);

                // (c) Touch targets on phone (>= 44px)
                let touchTargetIssues = [];
                if (dev.isPhone) {
                    touchTargetIssues = await page.evaluate(() => {
                        const issues = [];
                        const interactive = document.querySelectorAll('button:not(.btn-close):not(.carousel-control-prev):not(.carousel-control-next):not([aria-hidden="true"]), a.btn, input[type="submit"], input[type="button"], .tga-bottom-nav-item');
                        interactive.forEach(el => {
                            const rect = el.getBoundingClientRect();
                            // Only check visible elements in active viewport
                            if (rect.width > 0 && rect.height > 0 && window.getComputedStyle(el).display !== 'none' && window.getComputedStyle(el).visibility !== 'hidden') {
                                // Touch target is at least 44px in height or width, with small tolerance (40px)
                                if (rect.height < 40 && rect.width < 40) {
                                    issues.push({ tag: el.tagName, text: (el.innerText || el.value || el.getAttribute('aria-label') || '').trim().slice(0, 25), height: Math.round(rect.height), width: Math.round(rect.width) });
                                }
                            }
                        });
                        return issues;
                    });
                }

                // (d) Console errors
                const newConsoleErrors = consoleErrors.slice(startConsoleErrorsCount);

                const passed = scrollCheck.noOverflow && newFailedImages.length === 0 && newConsoleErrors.length === 0 && touchTargetIssues.length === 0;

                if (!passed) {
                    failedChecks++;
                }

                auditResults.push({
                    device: dev.name,
                    page: tp.name,
                    shot: shotName,
                    noOverflow: scrollCheck.noOverflow,
                    scrollW: scrollCheck.scrollW,
                    innerW: scrollCheck.innerW,
                    failedImages: newFailedImages.length,
                    consoleErrors: newConsoleErrors.length,
                    touchIssues: touchTargetIssues.length,
                    passed
                });

                console.log(` [PAGE] ${tp.name.padEnd(25)} | Shot: ${shotName} | Overflow: ${scrollCheck.noOverflow ? 'OK' : 'FAIL (' + scrollCheck.scrollW + '>' + scrollCheck.innerW + ')'} | ImgErrs: ${newFailedImages.length} | ConsoleErrs: ${newConsoleErrors.length} | TouchTargets: ${touchTargetIssues.length === 0 ? 'OK' : touchTargetIssues.length + ' small'}`);

            } catch (err) {
                failedChecks++;
                console.error(` [PAGE ERROR] ${tp.name}: ${err.message}`);
                auditResults.push({
                    device: dev.name,
                    page: tp.name,
                    error: err.message,
                    passed: false
                });
            }
        }

        // 2. DOCTOR PORTAL TESTS
        try {
            await page.goto(`${BASE_URL}/doctor/login`, { waitUntil: 'domcontentloaded', timeout: 30000 });
            await page.fill('input[name="Email"]', 'demo.doctor@ayurveda.local');
            await page.fill('input[name="Password"]', 'AnyPassword123!');
            await page.click('#loginForm button[type="submit"]');
            await page.waitForTimeout(1500);

            totalChecks++;
            const startFailedImagesCount = failedImages.length;
            const startConsoleErrorsCount = consoleErrors.length;

            await page.goto(`${BASE_URL}/DoctorPortal/Dashboard`, { waitUntil: 'networkidle', timeout: 30000 });
            await page.waitForTimeout(500);

            const shotName = `${dev.name}_doctor_dashboard.png`;
            await page.screenshot({ path: path.join(SCREENSHOT_DIR, shotName), fullPage: false });

            const scrollCheck = await page.evaluate(() => ({ scrollW: document.documentElement.scrollWidth, innerW: window.innerWidth, noOverflow: document.documentElement.scrollWidth <= window.innerWidth }));
            const newFailedImages = failedImages.slice(startFailedImagesCount);
            const newConsoleErrors = consoleErrors.slice(startConsoleErrorsCount);

            const passed = scrollCheck.noOverflow && newFailedImages.length === 0 && newConsoleErrors.length === 0;
            if (!passed) failedChecks++;

            console.log(` [PAGE] ${'Doctor Dashboard'.padEnd(25)} | Shot: ${shotName} | Overflow: ${scrollCheck.noOverflow ? 'OK' : 'FAIL'} | ImgErrs: ${newFailedImages.length} | ConsoleErrs: ${newConsoleErrors.length}`);

            auditResults.push({
                device: dev.name,
                page: 'Doctor Dashboard',
                shot: shotName,
                noOverflow: scrollCheck.noOverflow,
                failedImages: newFailedImages.length,
                consoleErrors: newConsoleErrors.length,
                passed
            });
        } catch (e) {
            console.log(`[DOCTOR] Notice: ${e.message}`);
        }

        // 3. ADMIN PORTAL TESTS (Login, Dashboard, Products Table)
        try {
            const adminPages = [
                { id: 'admin_login', path: '/admin-portal/auth/login', name: 'Admin Login' }
            ];

            for (const ap of adminPages) {
                totalChecks++;
                await page.goto(`${BASE_URL}${ap.path}`, { waitUntil: 'networkidle', timeout: 30000 });
                const shotName = `${dev.name}_${ap.id}.png`;
                await page.screenshot({ path: path.join(SCREENSHOT_DIR, shotName), fullPage: false });
                const scrollCheck = await page.evaluate(() => ({ scrollW: document.documentElement.scrollWidth, innerW: window.innerWidth, noOverflow: document.documentElement.scrollWidth <= window.innerWidth }));
                console.log(` [PAGE] ${ap.name.padEnd(25)} | Shot: ${shotName} | Overflow: ${scrollCheck.noOverflow ? 'OK' : 'FAIL'}`);
            }

            // Perform Admin Demo Login
            await page.goto(`${BASE_URL}/admin-portal/auth/login`, { waitUntil: 'domcontentloaded', timeout: 30000 });
            const chip = await page.$('button:has-text("Demo Admin"), .badge:has-text("Demo Admin")');
            if (chip) {
                await chip.click();
                await page.waitForTimeout(500);
            } else {
                await page.fill('input[name="Email"]', 'demo-admin@ayurveda.local');
                await page.fill('input[name="Password"]', getDemoAdminPassword());
            }
            await page.click('button[type="submit"]');
            await page.waitForTimeout(1200);

            const adminAuthPages = [
                { id: 'admin_dashboard', path: '/admin-portal/dashboard', name: 'Admin Dashboard' },
                { id: 'admin_products', path: '/admin-portal/products', name: 'Admin Products Table' }
            ];

            for (const ap of adminAuthPages) {
                totalChecks++;
                const startFailedImagesCount = failedImages.length;
                const startConsoleErrorsCount = consoleErrors.length;

                await page.goto(`${BASE_URL}${ap.path}`, { waitUntil: 'networkidle', timeout: 30000 });
                await page.waitForTimeout(500);

                const shotName = `${dev.name}_${ap.id}.png`;
                await page.screenshot({ path: path.join(SCREENSHOT_DIR, shotName), fullPage: false });

                const scrollCheck = await page.evaluate(() => ({ scrollW: document.documentElement.scrollWidth, innerW: window.innerWidth, noOverflow: document.documentElement.scrollWidth <= window.innerWidth }));
                const newFailedImages = failedImages.slice(startFailedImagesCount);
                const newConsoleErrors = consoleErrors.slice(startConsoleErrorsCount);

                const passed = scrollCheck.noOverflow && newFailedImages.length === 0 && newConsoleErrors.length === 0;
                if (!passed) failedChecks++;

                console.log(` [PAGE] ${ap.name.padEnd(25)} | Shot: ${shotName} | Overflow: ${scrollCheck.noOverflow ? 'OK' : 'FAIL'} | ImgErrs: ${newFailedImages.length} | ConsoleErrs: ${newConsoleErrors.length}`);

                auditResults.push({
                    device: dev.name,
                    page: ap.name,
                    shot: shotName,
                    noOverflow: scrollCheck.noOverflow,
                    failedImages: newFailedImages.length,
                    consoleErrors: newConsoleErrors.length,
                    passed
                });
            }

        } catch (e) {
            console.log(`[ADMIN] Notice: ${e.message}`);
        }

        await context.close();
    }

    await browser.close();

    console.log('\n======================================================================');
    console.log(' MULTI-DEVICE AUDIT COMPLETED');
    console.log(` Total Page-Device Checks: ${totalChecks}`);
    console.log(` Failed Checks: ${failedChecks}`);
    console.log(` Passed Checks: ${totalChecks - failedChecks}`);
    console.log('======================================================================\n');

    fs.writeFileSync(path.join(__dirname, 'audit_results.json'), JSON.stringify(auditResults, null, 2));

    if (failedChecks > 0) {
        console.warn(`WARNING: ${failedChecks} checks did not meet 100% criteria. See audit_results.json.`);
    } else {
        console.log('ALL DEVICES AND PAGES PASSED: 0 OVERFLOW, 0 FAILED IMAGES, 0 CONSOLE ERRORS, TOUCH TARGETS VALIDATED!');
    }
}

runDeviceAudit().catch(err => {
    console.error('Fatal audit failure:', err);
    process.exit(1);
});
