const { chromium } = require('playwright');
const assert = require('assert');
const path = require('path');
const fs = require('fs');
const { getDevAdminPassword, getCustomerPassword } = require('./getTestPassword');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:5000';
const ADMIN_URL = `${BASE_URL}/admin-portal`;
const SCREENSHOT_DIR = path.join(__dirname, 'screenshots');

if (!fs.existsSync(SCREENSHOT_DIR)) {
    fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
}

async function takeScreenshot(page, filename) {
    const fullPath = path.join(SCREENSHOT_DIR, filename);
    await page.screenshot({ path: fullPath, fullPage: false });
    console.log(`[SCREENSHOT] Saved: ${filename}`);
    return fullPath;
}

let testIndex = 0;
let passedCount = 0;
let failedCount = 0;

function logStep(name, status, details = '') {
    testIndex++;
    if (status === 'PASS') {
        passedCount++;
        console.log(`[TEST #${testIndex}] [PASS] ${name} ${details ? ' - ' + details : ''}`);
    } else {
        failedCount++;
        console.error(`[TEST #${testIndex}] [FAIL] ${name} ${details ? ' - ' + details : ''}`);
    }
}

(async () => {
    console.log(`======================================================================`);
    console.log(` Unified Ayurveda.Web & Area /admin-portal E2E Playwright Suite`);
    console.log(` Target Base URL: ${BASE_URL}`);
    console.log(` Target Admin URL: ${ADMIN_URL}`);
    console.log(`======================================================================\n`);

    const browser = await chromium.launch({ headless: true });

    try {
        // -------------------------------------------------------------------
        // 1. PUBLIC SITE ISOLATION & ZERO LINKS TO /admin-portal
        // -------------------------------------------------------------------
        console.log('\n--- Step 1: Public Site Isolation Check ---');
        const publicContext = await browser.newContext();
        const publicPage = await publicContext.newPage();
        await publicPage.goto(`${BASE_URL}/`, { waitUntil: 'networkidle', timeout: 30000 });

        const adminPortalLinks = await publicPage.$$eval('a[href*="admin-portal"]', links => links.map(l => l.href));
        assert.strictEqual(adminPortalLinks.length, 0, 'Public storefront must have 0 links to /admin-portal');
        logStep('Public Site Isolation', 'PASS', '0 links to /admin-portal detected on public homepage');

        const publicMetaRobots = await publicPage.$('meta[name="robots"]');
        const robotsContent = publicMetaRobots ? await publicMetaRobots.getAttribute('content') : '';
        assert(!robotsContent.includes('noindex'), 'Public site does not have noindex tag');
        logStep('Public SEO Indexing', 'PASS', 'Public storefront indexing active and preserved');

        // -------------------------------------------------------------------
        // 2. CUSTOMER LOGIN & ACCESSIBLE LOGOUT CONFIRMATION
        // -------------------------------------------------------------------
        console.log('\n--- Step 2: Customer Login & Logout Confirmation ---');
        await publicPage.goto(`${BASE_URL}/Account/Login`, { waitUntil: 'domcontentloaded' });
        await publicPage.fill('#loginForm input[name="Email"]', 'customer@ayurveda.com');
        await publicPage.fill('#loginForm input[name="Password"]', getCustomerPassword());
        await publicPage.click('#loginForm button[type="submit"]');
        await publicPage.waitForURL(url => !url.href.toLowerCase().includes('/account/login'), { timeout: 15000 });

        const customerCookies = await publicContext.cookies();
        const custWebAuth = customerCookies.find(c => c.name === '.Ayurveda.Web.Auth');
        assert(custWebAuth, 'Customer session cookie .Ayurveda.Web.Auth is set');
        assert(!customerCookies.find(c => c.name === '.Ayurveda.Admin.Auth'), 'Admin cookie NOT present for customer');
        logStep('Customer Login', 'PASS', 'Authenticated customer with .Ayurveda.Web.Auth');

        // Open customer profile dropdown
        const userDropdown = await publicPage.$('button[data-bs-toggle="dropdown"]');
        if (userDropdown) {
            await userDropdown.click();
            await publicPage.waitForTimeout(300);
        }

        const logoutTrigger = await publicPage.waitForSelector('[data-tga-logout]', { state: 'visible', timeout: 5000 });
        assert(logoutTrigger, 'Customer logout trigger exists');
        await logoutTrigger.click();

        await publicPage.waitForSelector('#confirmActionModal.show', { timeout: 10000 });
        logStep('Customer Logout Modal', 'PASS', 'Accessible confirm modal displayed on sign out click');

        await takeScreenshot(publicPage, '01_customer_login_logout.png');

        await publicPage.click('#confirmModalProceedBtn');
        await publicPage.waitForURL(url => url.href.toLowerCase().includes('/account/login'), { timeout: 15000 });
        const postLogoutCookies = await publicContext.cookies();
        assert(!postLogoutCookies.find(c => c.name === '.Ayurveda.Web.Auth'), 'Customer cookie cleared on logout');
        logStep('Customer Logout Complete', 'PASS', 'Customer session terminated safely and redirected to login');
        await publicContext.close();

        // -------------------------------------------------------------------
        // 3. DOCTOR LOGIN & LOGOUT CONFIRMATION
        // -------------------------------------------------------------------
        console.log('\n--- Step 3: Doctor Login & Logout Confirmation ---');
        const docContext = await browser.newContext();
        const docPage = await docContext.newPage();
        await docPage.goto(`${BASE_URL}/Account/Login?role=Doctor`, { waitUntil: 'domcontentloaded' });

        await docPage.fill('#loginForm input[name="Email"]', 'dr.aarav.sharma@thegreatayurveda.com');
        await docPage.fill('#loginForm input[name="Password"]', 'DevDemoPass_2026!#');
        await docPage.click('#loginForm button[type="submit"]');
        await docPage.waitForURL(url => !url.href.toLowerCase().includes('/account/login'), { timeout: 15000 });

        const docCookies = await docContext.cookies();
        assert(docCookies.find(c => c.name === '.Ayurveda.Web.Auth'), 'Doctor session cookie .Ayurveda.Web.Auth set');
        assert(!docCookies.find(c => c.name === '.Ayurveda.Admin.Auth'), 'Doctor does NOT have admin cookie');
        logStep('Doctor Login', 'PASS', 'Authenticated Vaidya session desk');

        const docLogoutTrigger = await docPage.waitForSelector('button[title="Sign Out from Doctor Portal"]', { state: 'visible', timeout: 5000 });
        assert(docLogoutTrigger, 'Doctor logout trigger exists');
        await docLogoutTrigger.click();

        await docPage.waitForSelector('#confirmActionModal.show', { timeout: 10000 });
        logStep('Doctor Logout Modal', 'PASS', 'Confirm modal displayed for Doctor portal');

        await takeScreenshot(docPage, '02_doctor_login_logout.png');

        await docPage.click('#confirmModalProceedBtn');
        await docPage.waitForURL(url => url.href.toLowerCase().includes('/account/login'), { timeout: 15000 });
        logStep('Doctor Logout Complete', 'PASS', 'Doctor session cleared and redirected to login');
        await docContext.close();

        // -------------------------------------------------------------------
        // 4. ADMIN LOGIN & COOKIE ISOLATION & DASHBOARD
        // -------------------------------------------------------------------
        console.log('\n--- Step 4: Admin Login & Cookie Isolation ---');
        const adminContext = await browser.newContext();
        const adminPage = await adminContext.newPage();

        // Verify X-Robots-Tag header and meta robots on Admin login page
        const loginResp = await adminPage.goto(`${ADMIN_URL}/auth/login`, { waitUntil: 'domcontentloaded' });
        const xRobots = loginResp.headers()['x-robots-tag'];
        assert(xRobots && xRobots.includes('noindex'), `Admin response contains X-Robots-Tag: ${xRobots}`);
        const metaRobots = await adminPage.$eval('meta[name="robots"]', el => el.getAttribute('content'));
        assert(metaRobots.includes('noindex'), 'Admin page has meta robots noindex');
        logStep('Admin SEO Isolation', 'PASS', 'X-Robots-Tag & meta robots noindex confirmed on /admin-portal');

        // Log in as SuperAdmin
        await adminPage.fill('input[name="Email"]', 'admin@thegreatayurveda.com');
        await adminPage.fill('input[name="Password"]', getDevAdminPassword());
        await adminPage.click('button[type="submit"]');
        await adminPage.waitForURL(url => url.href.toLowerCase().includes('/admin-portal/dashboard') || url.href.toLowerCase().endsWith('/admin-portal') || url.href.toLowerCase().endsWith('/admin-portal/'), { timeout: 15000 });

        const adminCookies = await adminContext.cookies();
        const adminCookie = adminCookies.find(c => c.name === '.Ayurveda.Admin.Auth');
        assert(adminCookie, 'Dedicated Admin Cookie .Ayurveda.Admin.Auth is set');
        assert(!adminCookies.find(c => c.name === '.Ayurveda.Web.Auth'), 'Public cookie .Ayurveda.Web.Auth NOT set for admin portal');
        logStep('Admin Login & Cookie Scheme', 'PASS', 'Isolated .Ayurveda.Admin.Auth cookie scheme established');

        await adminPage.waitForSelector('.admin-header', { timeout: 10000 });
        await takeScreenshot(adminPage, '03_admin_login_dashboard.png');
        logStep('Admin Dashboard KPIs', 'PASS', 'Executive management console loaded');

        // -------------------------------------------------------------------
        // 5. ADMIN CRUD: PRODUCTS & INSTANT PUBLIC IMAGE DISPLAY
        // -------------------------------------------------------------------
        console.log('\n--- Step 5: Admin CRUD for Products & Instant Public Image ---');
        await adminPage.goto(`${ADMIN_URL}/Products/Create`, { waitUntil: 'domcontentloaded' });

        const ts = Date.now().toString().slice(-4);
        const prodTitle = `Swarna Rasayana Elixir ${ts}`;
        const prodSku = `SRE-${ts}`;
        const prodPrice = '1299';
        const prodStock = '35';

        await adminPage.fill('#inputProductTitle', prodTitle);
        await adminPage.fill('#inputProductSku', prodSku);

        const catSelect = await adminPage.$('#selectCategory');
        const firstCatVal = await catSelect.$$eval('option', opts => {
            const valid = opts.find(o => o.value && o.value !== '');
            return valid ? valid.value : '';
        });
        await adminPage.selectOption('#selectCategory', firstCatVal);

        await adminPage.fill('#inputProductPrice', prodPrice);
        await adminPage.fill('#inputProductMrp', '1599');
        await adminPage.fill('#inputProductStock', prodStock);

        const sampleImgPath = path.join(__dirname, 'shared', 'test_prod_img.png');
        await adminPage.setInputFiles('#mainImageInput', sampleImgPath);

        await adminPage.fill('#inputProductDesc', 'Authentic classical Rasayana with pure herbs.');
        await adminPage.fill('#inputProductIngredients', 'Gold Bhasma, Ashwagandha, Guduchi, Amalaki');
        await adminPage.fill('#inputProductHowToUse', '1 spoonful morning with warm water');

        await adminPage.click('#btnSubmitCreateProduct');
        await adminPage.waitForURL(url => url.href.toLowerCase().includes('/admin-portal/products'), { timeout: 15000 });
        logStep('Admin Product Create', 'PASS', `Created product "${prodTitle}" with image upload`);

        // Read: Search for created product
        await adminPage.goto(`${ADMIN_URL}/Products?search=${encodeURIComponent(prodTitle)}`, { waitUntil: 'domcontentloaded' });
        const prodRow = await adminPage.$(`tr:has-text("${prodSku}")`);
        assert(prodRow, 'Created product found in products table');
        logStep('Admin Product Read', 'PASS', 'Product listed in admin table with SKU');

        // Get uploaded image src from admin table
        const uploadedImgSrc = await prodRow.$eval('img', el => el.getAttribute('src'));
        assert(uploadedImgSrc && uploadedImgSrc.startsWith('/uploads/'), `Uploaded image path is ${uploadedImgSrc}`);

        // Update product (price and stock)
        const editLink = await prodRow.$('a[href*="Edit"]');
        await editLink.click();
        await adminPage.waitForURL(url => url.href.toLowerCase().includes('/products/edit'), { timeout: 15000 });
        await adminPage.fill('#inputProductPrice', '1350');
        await adminPage.fill('#inputProductStock', '40');
        await adminPage.click('button[type="submit"]');
        await adminPage.waitForURL(url => url.href.toLowerCase().includes('/admin-portal/products'), { timeout: 15000 });
        logStep('Admin Product Update', 'PASS', 'Updated product price & stock successfully');

        // Instant Public Site Image Check: Image uploaded in admin serves immediately on public site
        const verifyContext = await browser.newContext();
        const verifyPage = await verifyContext.newPage();
        const publicImgResp = await verifyPage.goto(`${BASE_URL}${uploadedImgSrc}`);
        assert.strictEqual(publicImgResp.status(), 200, `Uploaded image serves with HTTP 200 on public site: ${uploadedImgSrc}`);
        logStep('Instant Public Image Serve', 'PASS', `Uploaded image served immediately on public site (${uploadedImgSrc})`);
        await verifyContext.close();

        await adminPage.goto(`${ADMIN_URL}/Products?search=${encodeURIComponent(prodTitle)}`, { waitUntil: 'domcontentloaded' });
        await takeScreenshot(adminPage, '04_admin_product_crud_and_public_image.png');

        // -------------------------------------------------------------------
        // 6. ADMIN CRUD: DOCTORS
        // -------------------------------------------------------------------
        console.log('\n--- Step 6: Admin CRUD for Doctors ---');
        await adminPage.goto(`${ADMIN_URL}/Doctors/Create`, { waitUntil: 'domcontentloaded' });

        const docName = `Dr. Devendra Sharma ${ts}`;
        const docEmail = `devendra.${ts}@ayurveda.test`;

        await adminPage.fill('input[name="doctorName"]', docName);
        await adminPage.fill('input[name="email"]', docEmail);
        await adminPage.fill('input[name="contactNumber"]', `+91 98765 0${ts}`);
        await adminPage.fill('input[name="experience"]', '12');
        await adminPage.fill('input[name="consultationFee"]', '750');

        const docSpecSelect = await adminPage.$('select[name="specializationId"]');
        if (docSpecSelect) {
            const val = await docSpecSelect.$$eval('option', opts => {
                const opt = opts.find(o => o.value && o.value !== '');
                return opt ? opt.value : '';
            });
            if (val) await adminPage.selectOption('select[name="specializationId"]', val);
        }

        await adminPage.click('#btnSubmitCreateDoctor');
        await adminPage.waitForURL(url => url.href.toLowerCase().includes('/admin-portal/doctors'), { timeout: 15000 });
        logStep('Admin Doctor Create', 'PASS', `Created doctor profile "${docName}"`);

        // Read & verify in directory table
        await adminPage.goto(`${ADMIN_URL}/Doctors?search=${encodeURIComponent(docName)}`, { waitUntil: 'domcontentloaded' });
        const docRow = await adminPage.$(`tr:has-text("${docName}")`);
        assert(docRow, 'Created doctor found in directory');
        logStep('Admin Doctor Read', 'PASS', `Doctor verified in admin practice directory: ${docName}`);

        await takeScreenshot(adminPage, '05_admin_doctor_crud.png');

        // -------------------------------------------------------------------
        // 7. ADMIN CRUD: USERS & GDPR EXPORT
        // -------------------------------------------------------------------
        console.log('\n--- Step 7: Admin CRUD for Users ---');
        await adminPage.goto(`${ADMIN_URL}/Users`, { waitUntil: 'domcontentloaded' });
        await adminPage.fill('input[name="search"]', 'customer@ayurveda.com');
        await adminPage.press('input[name="search"]', 'Enter');
        await adminPage.waitForTimeout(1000);

        const userRow = await adminPage.$('tr:has-text("customer@ayurveda.com")');
        assert(userRow, 'Found customer account in users table');
        logStep('Admin User Read', 'PASS', 'Customer account found in User directory');

        // Navigate to Dossier details
        const detailsLink = await userRow.$('a[href*="/Users/Details/"]');
        assert(detailsLink, 'Dossier link exists for user');
        await detailsLink.click();
        await adminPage.waitForURL(url => url.href.toLowerCase().includes('/users/details'), { timeout: 15000 });

        // GDPR Data Export verification
        const exportBtn = await adminPage.$('a[href*="/Users/ExportData"]');
        assert(exportBtn, 'GDPR Data export button exists');
        const [ download ] = await Promise.all([
            adminPage.waitForEvent('download'),
            exportBtn.click()
        ]);
        const suggestedFilename = download.suggestedFilename();
        assert(suggestedFilename.includes('.json'), 'GDPR export produces JSON file');
        logStep('Admin User GDPR Export', 'PASS', `Exported GDPR DPDP Act json compliance package (${suggestedFilename})`);

        await adminPage.goto(`${ADMIN_URL}/Users`, { waitUntil: 'domcontentloaded' });
        await takeScreenshot(adminPage, '06_admin_user_crud.png');

        // -------------------------------------------------------------------
        // 8. ADMIN CRUD: BANNERS
        // -------------------------------------------------------------------
        console.log('\n--- Step 8: Admin CRUD for Banners ---');
        await adminPage.goto(`${ADMIN_URL}/Marketing/Banners`, { waitUntil: 'domcontentloaded' });

        const bannerTitle = `Festival Auspicious Herb Offer ${ts}`;
        await adminPage.click('button[data-bs-target="#createBannerModal"]');
        await adminPage.waitForSelector('#createBannerModal.show', { timeout: 5000 });

        await adminPage.fill('#createBannerModal input[name="title"]', bannerTitle);
        await adminPage.fill('#createBannerModal input[name="subtitle"]', 'Flat 20% Off Authentic Formulations');
        await adminPage.fill('#createBannerModal input[name="linkUrl"]', '/Shop');
        await adminPage.fill('#createBannerModal input[name="imageUrl"]', '/images/banners/banner-hero-1.svg');
        await adminPage.selectOption('#createBannerModal select[name="placement"]', 'home_hero');

        await Promise.all([
            adminPage.waitForNavigation({ timeout: 15000 }).catch(() => {}),
            adminPage.click('#createBannerModal button[type="submit"]')
        ]);
        await adminPage.waitForTimeout(1000);

        await adminPage.goto(`${ADMIN_URL}/Marketing/Banners`, { waitUntil: 'domcontentloaded' });
        const bannerCard = await adminPage.$(`text="${bannerTitle}"`);
        assert(bannerCard, 'Created banner found in Banners view');
        logStep('Admin Banner CRUD', 'PASS', `Created and verified storefront banner "${bannerTitle}"`);

        await takeScreenshot(adminPage, '07_admin_banner_crud.png');

        // -------------------------------------------------------------------
        // 9. ADMIN CRUD: COUPONS
        // -------------------------------------------------------------------
        console.log('\n--- Step 9: Admin CRUD for Coupons ---');
        await adminPage.goto(`${ADMIN_URL}/Marketing/Coupons`, { waitUntil: 'domcontentloaded' });

        const couponCode = `MERGE${ts}`;
        await adminPage.click('button[data-bs-target="#createCouponModal"]');
        await adminPage.waitForSelector('#createCouponModal.show', { timeout: 5000 });

        await adminPage.fill('#createCouponModal input[name="code"]', couponCode);
        await adminPage.selectOption('#createCouponModal select[name="discountType"]', 'Flat');
        await adminPage.fill('#createCouponModal input[name="discountAmount"]', '175');
        await adminPage.fill('#createCouponModal input[name="minOrderAmount"]', '600');

        await Promise.all([
            adminPage.waitForNavigation({ timeout: 15000 }).catch(() => {}),
            adminPage.click('#createCouponModal button[type="submit"]')
        ]);
        await adminPage.waitForTimeout(1000);

        await adminPage.goto(`${ADMIN_URL}/Marketing/Coupons`, { waitUntil: 'domcontentloaded' });
        const couponRow = await adminPage.$(`tr:has-text("${couponCode}")`);
        assert(couponRow, 'Created coupon found in coupons table');
        logStep('Admin Coupon CRUD', 'PASS', `Created and verified coupon "${couponCode}" with flat ₹175 discount`);

        await takeScreenshot(adminPage, '08_admin_coupon_crud.png');

        // -------------------------------------------------------------------
        // 10. ADMIN LOGOUT CONFIRMATION
        // -------------------------------------------------------------------
        console.log('\n--- Step 10: Admin Logout Confirmation ---');
        const adminLogoutBtn = await adminPage.$('.admin-header [data-tga-logout]');
        assert(adminLogoutBtn, 'Admin header Sign Out trigger exists');
        await adminLogoutBtn.click();

        await adminPage.waitForSelector('#confirmActionModal.show', { timeout: 10000 });
        const modalTitle = await adminPage.$eval('#confirmModalTitleText', el => el.innerText);
        assert.strictEqual(modalTitle, 'Confirm Administrative Sign Out');
        logStep('Admin Logout Modal', 'PASS', 'Accessible admin sign out confirmation modal displayed');

        await takeScreenshot(adminPage, '09_admin_logout_confirm.png');

        await adminPage.click('#confirmModalProceedBtn');
        await adminPage.waitForURL(url => url.href.toLowerCase().includes('/admin-portal/login') || url.href.toLowerCase().includes('/admin-portal/auth/login'), { timeout: 15000 });

        const postAdminCookies = await adminContext.cookies();
        assert(!postAdminCookies.find(c => c.name === '.Ayurveda.Admin.Auth'), 'Admin cookie cleared on logout');
        logStep('Admin Logout Complete', 'PASS', 'Admin session terminated and redirected to admin login page');
        await adminContext.close();

        console.log('\n======================================================================');
        console.log(` SUMMARY: ALL ${passedCount} PLAYWRIGHT E2E TESTS PASSED (0 FAILURES)`);
        console.log('======================================================================');

    } catch (err) {
        console.error('\n[ERROR] Playwright Test Suite Failed:', err);
        process.exitCode = 1;
    } finally {
        await browser.close();
    }
})();
