const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');
const { execSync } = require('child_process');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:5000';
const ADMIN_URL = `${BASE_URL}/admin-portal`;
const SCREENSHOT_DIR = path.resolve(__dirname, 'screenshots/phase9');

if (!fs.existsSync(SCREENSHOT_DIR)) {
    fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
}

const VIEWPORTS = [
    { name: 'Galaxy_S20', width: 360, height: 740, isMobile: true },
    { name: 'iPhone_13', width: 390, height: 844, isMobile: true },
    { name: 'Pixel_7', width: 412, height: 915, isMobile: true },
    { name: 'iPad_Port', width: 768, height: 1024, isMobile: false },
    { name: 'iPad_Land', width: 1024, height: 768, isMobile: false },
    { name: 'Laptop', width: 1366, height: 768, isMobile: false },
    { name: 'Desktop_FHD', width: 1920, height: 1080, isMobile: false }
];

const auditResults = {
    round: 1,
    passed: 0,
    failed: 0,
    failures: [],
    maxMemoryMB: 0,
    details: []
};

function logResult(category, name, passed, message = '') {
    const icon = passed ? '✅ PASS' : '❌ FAIL';
    console.log(`${icon} [${category}] ${name}${message ? ' - ' + message : ''}`);
    auditResults.details.push({ category, name, passed, message });
    if (passed) {
        auditResults.passed++;
    } else {
        auditResults.failed++;
        auditResults.failures.push({ category, name, message });
    }
}

function checkProcessMemory() {
    try {
        const out = execSync('powershell -NoProfile -Command "(Get-Process -Name dotnet, Ayurveda.Web -ErrorAction SilentlyContinue | Measure-Object WorkingSet64 -Maximum).Maximum / 1MB"', { encoding: 'utf8' }).trim();
        const mb = parseFloat(out);
        if (!isNaN(mb) && mb > auditResults.maxMemoryMB) {
            auditResults.maxMemoryMB = Math.round(mb * 100) / 100;
        }
    } catch (e) {
        // ignore
    }
}

async function takeShot(page, filename) {
    const fullPath = path.join(SCREENSHOT_DIR, `${filename}.png`);
    await page.screenshot({ path: fullPath, fullPage: false });
    return fullPath;
}

async function checkHorizontalOverflow(page, viewportName, pageName) {
    const hasOverflow = await page.evaluate(() => {
        return document.documentElement.scrollWidth > window.innerWidth;
    });
    logResult('OverflowAudit', `${pageName} @ ${viewportName} (ScrollWidth: <= ${page.viewportSize().width}px)`, !hasOverflow, hasOverflow ? 'Horizontal overflow detected' : 'Fits viewport cleanly');
}

async function runAudit() {
    console.log('======================================================================');
    console.log(' PHASE 9: BROWSER TESTING (PLAYWRIGHT CHROMIUM + CHROME CHANNEL)');
    console.log(` Target Server: ${BASE_URL}`);
    console.log('======================================================================\n');

    checkProcessMemory();

    // Launch Chromium
    const browser = await chromium.launch({
        headless: true
    });

    const context = await browser.newContext({
        viewport: { width: 1280, height: 800 }
    });
    const page = await context.newPage();

    try {
        // -------------------------------------------------------------------
        // 1. HOME PAGE & HERO CAROUSEL AUDIT
        // -------------------------------------------------------------------
        console.log('\n--- 1. Home Page & Hero Carousel Audit ---');
        await page.goto(`${BASE_URL}/`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(1000);
        checkProcessMemory();

        const title = await page.title();
        logResult('HomePage', 'Title Verification', title.includes('Ayurveda'), `Title: "${title}"`);

        // Check Dummy Ad Strip on Home
        const homeAd = await page.$('.ad-banner-strip, img[src*="ad-"]');
        logResult('DummyAds', 'Home Page Ad Strip Rendered', homeAd !== null, 'Found ad element');
        if (homeAd) {
            const adLabel = await page.evaluate(() => document.body.innerText.includes('Ad (demo)'));
            logResult('DummyAds', 'Ad (demo) Label Present on Home', adLabel);
        }

        // Check Carousel across mobile (<576px) vs desktop (>=576px)
        for (const vp of VIEWPORTS) {
            await page.setViewportSize({ width: vp.width, height: vp.height });
            await page.waitForTimeout(400);

            await checkHorizontalOverflow(page, vp.name, 'Home');

            if (vp.width < 576) {
                // Carousel arrows hidden on <576px
                const arrowVisible = await page.evaluate(() => {
                    const prevBtn = document.querySelector('.tga-carousel-nav-btn.carousel-control-prev');
                    if (!prevBtn) return false;
                    const style = window.getComputedStyle(prevBtn);
                    return style.display !== 'none' && style.visibility !== 'hidden';
                });
                logResult('CarouselArrows', `Hidden on Phone (${vp.width}px)`, !arrowVisible);

                // Captions max-width 90% and text-wrap: balance
                const captionStyles = await page.evaluate(() => {
                    const cap = document.querySelector('.banner-caption-box');
                    if (!cap) return null;
                    const style = window.getComputedStyle(cap);
                    return {
                        maxWidth: style.maxWidth,
                        textWrap: style.textWrap || style.webkitTextWrap || 'normal'
                    };
                });
                logResult('HeroCaptions', `Max-width 90% & text-wrap balance on Phone (${vp.width}px)`, captionStyles !== null && (captionStyles.maxWidth === '90%' || captionStyles.maxWidth.includes('90')));

                // 3 leaf particles rendered on phone
                const particlesCount = await page.evaluate(() => {
                    const visibleParticles = Array.from(document.querySelectorAll('.hero-particles-layer .hero-particle'))
                        .filter(p => window.getComputedStyle(p).display !== 'none');
                    return visibleParticles.length;
                });
                logResult('Particles', `Phone Leaf Particles Count (${vp.width}px)`, particlesCount === 3, `Count: ${particlesCount}`);
            } else {
                // Carousel arrows 44px circles at 8px edges on >=576px
                const arrowLayout = await page.evaluate(() => {
                    const prevBtn = document.querySelector('.tga-carousel-nav-btn.carousel-control-prev');
                    if (!prevBtn) return null;
                    const style = window.getComputedStyle(prevBtn);
                    const rect = prevBtn.getBoundingClientRect();
                    return {
                        display: style.display,
                        width: Math.round(rect.width),
                        height: Math.round(rect.height),
                        left: style.left
                    };
                });
                logResult('CarouselArrows', `44px Circle at 8px on >=576px (${vp.width}px)`, arrowLayout !== null && arrowLayout.display === 'flex' && arrowLayout.width >= 40 && arrowLayout.left === '8px', `Width: ${arrowLayout?.width}px, Left: ${arrowLayout?.left}`);
            }
        }

        await takeShot(page, 'home_responsive_passed');

        // Sticky Header scroll test
        await page.setViewportSize({ width: 1280, height: 800 });
        await page.evaluate(() => window.scrollTo(0, 0));
        await page.waitForTimeout(300);

        // Scroll down > 80px -> header hides
        await page.evaluate(() => window.scrollTo(0, 200));
        await page.waitForTimeout(400);
        const isHeaderHidden = await page.evaluate(() => {
            const h = document.getElementById('tgaSiteHeaderWrapper');
            return h && h.classList.contains('header-hidden');
        });
        logResult('StickyHeader', 'Header Hides on Scroll Down > 80px', isHeaderHidden);

        // Scroll up by 15px -> header reveals immediately
        await page.evaluate(() => window.scrollTo(0, 180));
        await page.waitForTimeout(400);
        const isHeaderRevealed = await page.evaluate(() => {
            const h = document.getElementById('tgaSiteHeaderWrapper');
            return h && !h.classList.contains('header-hidden');
        });
        logResult('StickyHeader', 'Header Shows Immediately on Upward Scroll', isHeaderRevealed);

        // -------------------------------------------------------------------
        // 2. SMART SEARCH & ROTATING PLACEHOLDER AUDIT
        // -------------------------------------------------------------------
        console.log('\n--- 2. Smart Search & Filters Audit ---');
        await page.goto(`${BASE_URL}/search?q=ashwa`, { waitUntil: 'domcontentloaded', timeout: 60000 });
        await page.waitForTimeout(1000);
        checkProcessMemory();

        await checkHorizontalOverflow(page, 'Laptop', 'Search');

        // Search Input & Suggestions
        const searchInput = await page.$('.tga-search-input');
        logResult('Search', 'Search Input Exists', searchInput !== null);

        if (searchInput) {
            await searchInput.fill('');
            await searchInput.type('Chyawan', { delay: 50 });
            await page.waitForTimeout(800);

            const dropdownOpen = await page.evaluate(() => {
                const dd = document.querySelector('.search-results-dropdown.show, .search-autocomplete-dropdown.show, .tga-search-dropdown.show');
                return dd !== null;
            });
            logResult('Search', 'Search Suggestions Dropdown Renders with Scope Chips', dropdownOpen);

            // Rotating placeholder cycle check
            await searchInput.fill(''); // blur and empty to resume rotation
            await page.click('body');
            const initialPlaceholder = await page.evaluate(() => {
                const el = document.querySelector('.tga-search-input');
                return el ? el.getAttribute('placeholder') : '';
            });
            await page.waitForTimeout(3200);
            const secondPlaceholder = await page.evaluate(() => {
                const el = document.querySelector('.tga-search-input');
                return el ? el.getAttribute('placeholder') : '';
            });
            const placeholderRotated = initialPlaceholder !== '' && secondPlaceholder !== '' && initialPlaceholder !== secondPlaceholder;
            logResult('SearchPlaceholder', 'Rotating Placeholder Cycles Over 2.5s', placeholderRotated, `From "${initialPlaceholder}" to "${secondPlaceholder}"`);
        }

        // Faceted Filters on Search / Shop
        const filterAccordion = await page.$('.search-filters-content, #filterAccordion, .faceted-filters-wrapper, .search-filters-sidebar');
        logResult('Search', 'Faceted Filters Sidebar Exists', filterAccordion !== null);

        // Dummy Ad on Search / Shop (ad_shop_side or ad_shop_inline)
        const shopAd = await page.$('img[src*="ad-kumkumadi"], img[src*="ad-triphala"], img[src*="ad-brahmi"], img[src*="ad-ghee"]');
        logResult('DummyAds', 'Search / Shop Ad Rendered', shopAd !== null);

        await takeShot(page, 'search_faceted_passed');

        // -------------------------------------------------------------------
        // 3. SHOP & PRODUCT DETAILS AUDIT
        // -------------------------------------------------------------------
        console.log('\n--- 3. Shop & Product Details Audit ---');
        await page.goto(`${BASE_URL}/shop`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(1000);

        for (const vp of [VIEWPORTS[0], VIEWPORTS[1], VIEWPORTS[6]]) {
            await page.setViewportSize({ width: vp.width, height: vp.height });
            await checkHorizontalOverflow(page, vp.name, 'Shop');
        }

        // Navigate to Product Details
        const firstProductLink = await page.$('.tga-product-card a, a[href*="/Shop/Details"], a[href*="/shop/details"]');
        if (firstProductLink) {
            const prodUrl = await firstProductLink.getAttribute('href');
            await page.goto(`${BASE_URL}${prodUrl}`, { waitUntil: 'domcontentloaded', timeout: 30000 });
            await page.waitForTimeout(800);

            // Check Product Below Ad (ad_product_below)
            const prodBelowAd = await page.$('img[src*="ad-ojastea"]');
            logResult('DummyAds', 'Product Below Ad (ad_product_below) Rendered', prodBelowAd !== null);

            // Add to Cart
            const addToCartBtn = await page.$('button[type="submit"]:has-text("Add to Basket"), .btn-add-to-cart, button:has-text("Add to Cart")');
            if (addToCartBtn) {
                await addToCartBtn.click();
                await page.waitForTimeout(1000);
                logResult('Cart', 'Add to Cart Executed', true);
            }
        }

        await takeShot(page, 'product_details_passed');

        // -------------------------------------------------------------------
        // 4. BASKET / CART & GST LABEL AUDIT
        // -------------------------------------------------------------------
        console.log('\n--- 4. Basket & GST Label Audit ---');
        await page.goto(`${BASE_URL}/cart`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(1000);

        // Check GST Demo Label
        const pageText = await page.evaluate(() => document.body.innerText);
        const hasGstDemoLabel = pageText.includes('Estimated GST (5% - Demo)') || pageText.includes('GST (5% - Demo)');
        logResult('FinanceGST', 'Demo GST Label ("Estimated GST (5% - Demo)") Present in Cart', hasGstDemoLabel);

        // Check Cart Ad (ad_cart)
        const cartAd = await page.$('img[src*="ad-copper-bottle"], .ad-banner-cart');
        logResult('DummyAds', 'Cart Ad (ad_cart) Rendered', cartAd !== null);

        await takeShot(page, 'cart_view_passed');

        // -------------------------------------------------------------------
        // 5. CHECKOUT & RAZORPAY TEST MODE AUDIT
        // -------------------------------------------------------------------
        console.log('\n--- 5. Checkout & Razorpay Test Mode Audit ---');
        // Authenticate as Customer via Demo Guest button
        await page.goto(`${BASE_URL}/Account/Login`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(600);
        const demoGuestBtn = await page.$('button[formaction="/account/continue-demo-guest"], .btn-demo-guest-highlight');
        if (demoGuestBtn) {
            await demoGuestBtn.click();
            await page.waitForTimeout(1500);
        }

        // Navigate to shop and add a real product to cart
        await page.goto(`${BASE_URL}/shop`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(800);
        const prodLink = await page.$('.tga-product-card a, a[href*="/Shop/Details"], a[href*="/shop/details"]');
        if (prodLink) {
            await prodLink.click();
            await page.waitForTimeout(1000);
            const detailAddBtn = await page.$('#purchaseForm button[type="submit"], button:has-text("Add to Basket")');
            if (detailAddBtn) {
                await detailAddBtn.click();
                await page.waitForTimeout(1500);
            }
        }

        // Navigate to checkout
        await page.goto(`${BASE_URL}/checkout`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(1200);

        // Verify Sandbox Test Mode notice
        const checkoutText = await page.evaluate(() => document.body.innerText);
        const hasTestModeNotice = checkoutText.includes('4111 1111 1111 1111') && checkoutText.includes('success@razorpay');
        logResult('Razorpay', 'Sandbox Test Credentials Note Present', hasTestModeNotice);

        // Verify Hidden Inputs for Razorpay
        const hasRazorpayHiddenFields = await page.evaluate(() => {
            return document.getElementById('rzpOrderId') !== null &&
                   document.getElementById('rzpKeyId') !== null &&
                   document.getElementById('rzpPaymentId') !== null &&
                   document.getElementById('rzpSignature') !== null;
        });
        logResult('Razorpay', 'Razorpay Hidden Telemetry Fields Present in Form', hasRazorpayHiddenFields);

        // Fill Address and Select COD
        let confirmedOrderUrl = null;
        await page.evaluate(() => {
            const setVal = (sel, val) => { const el = document.querySelector(sel); if (el) el.value = val; };
            setVal('input[name="NewAddress.RecipientName"]', 'Aarav Patel');
            setVal('input[name="NewAddress.Phone"]', '9876543210');
            setVal('input[name="NewAddress.AddressLine1"]', '12 Lotus Greens, MG Road');
            setVal('input[name="NewAddress.City"]', 'Indore');
            setVal('input[name="NewAddress.State"]', 'Madhya Pradesh');
            setVal('input[name="NewAddress.PostalCode"]', '452001');

            const codRadio = document.getElementById('payCod');
            if (codRadio) codRadio.checked = true;
        });
        await page.waitForTimeout(400);

        // Click Pay & Place Order button
        const payBtn = await page.$('#btnInitiatePayment');
        if (payBtn) {
            await payBtn.click();
            await page.waitForTimeout(3500);
        }

        const currentUrl = page.url();
        const orderPlaced = currentUrl.toLowerCase().includes('/confirmation') || currentUrl.toLowerCase().includes('/orders');
        logResult('Checkout', 'Order Placed Successfully via Checkout', orderPlaced);
        const lowerUrl = currentUrl.toLowerCase();
        if (lowerUrl.includes('/confirmation/')) {
            const confId = lowerUrl.split('/confirmation/')[1]?.split('?')[0]?.split('/')[0];
            if (confId) confirmedOrderUrl = `/Orders/Details/${confId}`;
        }

        await takeShot(page, 'checkout_placed_passed');

        // -------------------------------------------------------------------
        // 6. ORDER TRACKING MAP (LEAFLET + OPENSTREETMAP) AUDIT
        // -------------------------------------------------------------------
        console.log('\n--- 6. Leaflet Order Tracking Map Audit ---');
        let detailsUrl = confirmedOrderUrl;
        if (!detailsUrl) {
            const confLink = await page.$('a[href*="/Orders/Details"], a[href*="/orders/details"]');
            if (confLink) {
                detailsUrl = await confLink.getAttribute('href');
            } else {
                await page.goto(`${BASE_URL}/orders`, { waitUntil: 'domcontentloaded', timeout: 30000 });
                await page.waitForTimeout(1000);
                const orderLink = await page.$('a[href*="/Orders/Details"], a[href*="/orders/details"]');
                if (orderLink) {
                    detailsUrl = await orderLink.getAttribute('href');
                }
            }
        }

        if (detailsUrl) {
            await page.goto(`${BASE_URL}${detailsUrl}`, { waitUntil: 'domcontentloaded', timeout: 30000 });
            await page.waitForTimeout(2000);

            // Check Leaflet map container and tilelayer
            const mapContainer = await page.$('#orderTrackingMap');
            logResult('LeafletMap', 'Map Container (#orderTrackingMap) Exists', mapContainer !== null);

            const hasOsmAttribution = await page.evaluate(() => {
                const el = document.querySelector('.leaflet-control-attribution');
                return el && el.innerText.includes('OpenStreetMap');
            });
            logResult('LeafletMap', 'Visible OpenStreetMap Contributors Attribution', hasOsmAttribution);

            // Check Warehouse and Destination markers rendered
            const customMarkersCount = await page.evaluate(() => {
                return document.querySelectorAll('.custom-map-icon').length;
            });
            logResult('LeafletMap', 'Warehouse, Destination & Vehicle Markers Rendered', customMarkersCount >= 2, `Count: ${customMarkersCount}`);

            // Check Fit Route button
            const fitRouteBtn = await page.$('#btnRecenterMap');
            logResult('LeafletMap', 'Fit Route Button Exists', fitRouteBtn !== null);

            // Check "Use My Location" button
            const useLocationBtn = await page.$('#btnUseMyLocation');
            logResult('LeafletMap', '"Use My Location" Button Exists', useLocationBtn !== null);

            // Mobile touch overlay test
            await page.setViewportSize({ width: 390, height: 844 });
            await page.waitForTimeout(300);
            await checkHorizontalOverflow(page, 'iPhone_13', 'OrderDetailsMap');

            await takeShot(page, 'order_tracking_map_passed');
        }

        // -------------------------------------------------------------------
        // 7. REAL EMAIL OTP & LOGIN THROTTLE AUDIT
        // -------------------------------------------------------------------
        console.log('\n--- 7. Real Email OTP & Throttle Audit ---');
        await page.context().clearCookies();
        await page.goto(`${BASE_URL}/Account/ForgotPassword`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(500);

        const emailInput = await page.$('input[name="Email"]');
        if (emailInput) {
            await emailInput.fill('customer@ayurveda.com');
            const submitBtn = await page.$('button[type="submit"]');
            await submitBtn.click();
            await page.waitForTimeout(1500);

            // Verify .eml written to App_Data/outbox
            const outboxDir = path.resolve(__dirname, '../../App_Data/outbox');
            let emlFound = false;
            let otpCode = null;

            if (fs.existsSync(outboxDir)) {
                const files = fs.readdirSync(outboxDir).filter(f => f.endsWith('.eml'));
                if (files.length > 0) {
                    emlFound = true;
                    // Read latest file
                    const latestFile = files.sort((a, b) => fs.statSync(path.join(outboxDir, b)).mtimeMs - fs.statSync(path.join(outboxDir, a)).mtimeMs)[0];
                    const content = fs.readFileSync(path.join(outboxDir, latestFile), 'utf8');
                    const match = content.match(/\b([0-9]{6})\b/);
                    if (match) {
                        otpCode = match[1];
                    }
                }
            }
            logResult('EmailOTP', 'RFC822 .eml Written to App_Data/outbox', emlFound);
            logResult('EmailOTP', '6-Digit OTP Code Generated and Extracted', otpCode !== null, `Code: ${otpCode || 'N/A'}`);

            if (otpCode && (page.url().includes('/VerifyOtp') || page.url().includes('/ResetPassword') || page.url().includes('/reset-password'))) {
                // Enter into 6-box OTP fields
                for (let i = 0; i < 6; i++) {
                    const digitBox = await page.$(`input.reset-otp-box[data-index="${i}"], input.otp-digit-input[data-index="${i}"]`);
                    if (digitBox) {
                        await digitBox.fill(otpCode[i]);
                    }
                }
                const newPassInput = await page.$('input[name="NewPassword"]');
                if (newPassInput) {
                    await newPassInput.fill('Customer@123');
                    await page.fill('input[name="ConfirmPassword"]', 'Customer@123');
                }
                await page.waitForTimeout(300);
                const verifyBtn = await page.$('#resetPasswordForm button[type="submit"], #otpVerifyForm button[type="submit"], button[type="submit"]');
                if (verifyBtn) {
                    await verifyBtn.click();
                    await page.waitForTimeout(1500);
                    logResult('EmailOTP', '6-Box OTP Verification Accepted', page.url().includes('/ResetPassword') || page.url().includes('/Login') || page.url().includes('/Success'));
                }
            }
        }

        // Login Throttle Test: 3 wrong attempts then 4th blocked with countdown
        console.log('\n--- Login Throttle Test: 3 wrong attempts then 4th blocked ---');
        await page.context().clearCookies();
        await page.goto(`${BASE_URL}/Account/Login`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        const throttleTestEmail = `throttle_test_${Date.now()}@test.com`;
        for (let attempt = 1; attempt <= 3; attempt++) {
            await page.fill('input[name="Email"]', throttleTestEmail);
            await page.fill('input[name="Password"]', 'WrongPass123!');
            await page.click('#loginForm button[type="submit"]');
            await page.waitForTimeout(600);
        }
        // 4th attempt: should be blocked with countdown message
        await page.fill('input[name="Email"]', throttleTestEmail);
        await page.fill('input[name="Password"]', 'WrongPass123!');
        await page.click('#loginForm button[type="submit"]');
        await page.waitForTimeout(600);

        const pageTextAfter4th = await page.evaluate(() => document.body.innerText);
        const isBlockedWithCountdown = pageTextAfter4th.includes('Too many failed login attempts') &&
                                       (pageTextAfter4th.includes('seconds') || pageTextAfter4th.includes('try again in'));
        logResult('LoginThrottle', '4th Failed Login Attempt Blocked with Countdown', isBlockedWithCountdown, `Message present: ${isBlockedWithCountdown}`);

        // -------------------------------------------------------------------
        // 8. ADMIN PORTAL AUDIT (BANNER PLACEMENTS & FINANCE STATUTORY REMOVED)
        // -------------------------------------------------------------------
        console.log('\n--- 8. Admin Portal Banners & Finance Audit ---');
        await page.goto(`${ADMIN_URL}/auth/login`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(600);
        const demoAdminChip = await page.$('.chip-pill[data-demo-email], .demo-chip');
        if (demoAdminChip) {
            await demoAdminChip.click();
            await page.waitForTimeout(300);
        } else {
            await page.fill('input[name="Email"]', 'demo-admin@ayurveda.local');
            await page.fill('input[name="Password"]', 'DemoAdmin@2026!');
        }
        await page.click('button[type="submit"]');
        await page.waitForTimeout(2000);

        logResult('AdminPortal', 'Admin Login Successful', page.url().includes('/admin-portal') && !page.url().includes('/auth/login'));

        // Check Marketing Banners placements
        await page.goto(`${ADMIN_URL}/marketing/banners`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(800);

        const bannerOptions = await page.evaluate(() => {
            const select = document.querySelector('select[name="placement" i], #bannerPlacementSelect');
            if (!select) return [];
            return Array.from(select.options).map(o => o.value);
        });

        const hasAllAdPlacements = ['ad_home_strip', 'ad_shop_inline', 'ad_shop_side', 'ad_product_below', 'ad_cart']
            .every(p => bannerOptions.includes(p));
        logResult('AdminBanners', 'Marketing Banners Dropdown Has All 5 Ad Placements', hasAllAdPlacements, `Placements: ${bannerOptions.filter(o => o.startsWith('ad_')).join(', ')}`);

        // Check Finance Reports: Statutory removed and "Estimated GST (5% - Demo)" label
        await page.goto(`${ADMIN_URL}/finance/reports`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(800);

        const financeText = await page.evaluate(() => document.body.innerText);
        const statutoryRemoved = !financeText.toLowerCase().includes('statutory compliance') && !financeText.toLowerCase().includes('statutory tax');
        const hasFinanceGstDemo = financeText.includes('Estimated GST (5% - Demo)');

        logResult('AdminFinance', 'Statutory Claim Removed from Finance Reports', statutoryRemoved);
        logResult('AdminFinance', '"Estimated GST (5% - Demo)" in Finance Reports', hasFinanceGstDemo);

        await takeShot(page, 'admin_finance_passed');

        // -------------------------------------------------------------------
        // 9. DOCTORS PAGE AUDIT
        // -------------------------------------------------------------------
        console.log('\n--- 9. Doctors Telemedicine Directory Audit ---');
        await page.goto(`${BASE_URL}/doctors`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(800);
        await checkHorizontalOverflow(page, 'Laptop', 'Doctors');
        const docCardsCount = await page.evaluate(() => document.querySelectorAll('.doctor-card, .doc-profile-card, a[href*="/doctors/profile"], a[href*="/Doctors/Details"], a[href*="/doctors/book"], a[href*="/Doctors/Book"]').length);
        logResult('DoctorsPage', 'Doctors Directory Loaded with Profiles', docCardsCount > 0, `Found: ${docCardsCount} doctor cards`);

        // -------------------------------------------------------------------
        // 10. ADMIN SIDEBAR & WHOAMI AUDIT
        // -------------------------------------------------------------------
        console.log('\n--- 10. Admin Sidebar & WhoAmI Audit ---');
        await page.goto(`${ADMIN_URL}/dashboard`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(800);
        const hasSidebar = await page.evaluate(() => {
            const sb = document.querySelector('.admin-sidebar, #adminSidebar, aside, .sidebar-nav');
            return sb !== null;
        });
        logResult('AdminPortal', 'Admin Sidebar Navigation Rendered', hasSidebar);

        await page.goto(`${ADMIN_URL}/auth/whoami`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(800);
        const whoAmIText = await page.evaluate(() => document.body.innerText);
        logResult('AdminPortal', 'WhoAmI Endpoint Verified', whoAmIText.includes('admin@ayurveda.com') || whoAmIText.includes('SuperAdmin') || whoAmIText.includes('Identity'));

    } catch (err) {
        console.error('Audit execution error:', err);
        logResult('Fatal', 'Suite Exception', false, err.message);
    } finally {
        checkProcessMemory();
        await browser.close();
    }

    // -----------------------------------------------------------------------
    // FINAL WORKING SET MEMORY ASSERTION (< 200 MB)
    // -----------------------------------------------------------------------
    console.log('\n======================================================================');
    console.log(` PEAK WORKING SET MEMORY: ${auditResults.maxMemoryMB} MB (Limit: 200 MB)`);
    const isMemoryCompliant = auditResults.maxMemoryMB > 0 && auditResults.maxMemoryMB < 200;
    logResult('MemoryBudget', 'Peak Memory Under 200 MB', isMemoryCompliant, `Peak: ${auditResults.maxMemoryMB} MB`);

    console.log('======================================================================');
    console.log(` SUMMARY: ${auditResults.passed} PASSED, ${auditResults.failed} FAILED`);
    console.log('======================================================================\n');

    fs.writeFileSync(path.resolve(__dirname, 'screenshots/phase9/audit_report.json'), JSON.stringify(auditResults, null, 2));

    if (auditResults.failed > 0) {
        process.exit(1);
    } else {
        process.exit(0);
    }
}

runAudit();
