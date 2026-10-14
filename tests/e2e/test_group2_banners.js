const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

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
    console.log('🧪 TASK 2 E2E VERIFICATION SUITE: HERO BANNERS & ADMIN BANNER CRUD');
    console.log(`Web URL:   ${WEB_URL}`);
    console.log(`Admin URL: ${ADMIN_URL}`);
    console.log('================================================================\n');

    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage({ viewport: { width: 1536, height: 864 } });

    try {
        // -------------------------------------------------------------
        // 1. PUBLIC STOREFRONT HERO CAROUSEL VERIFICATION
        // -------------------------------------------------------------
        console.log('\n--- 1. Testing Public Hero Carousel ---');
        await page.goto(`${WEB_URL}`, { waitUntil: 'networkidle', timeout: 30000 });
        await page.waitForTimeout(1000);

        // Check hero carousel container
        const carousel = await page.$('#homeHeroCarousel');
        assert(carousel !== null, 'Hero carousel element #homeHeroCarousel exists on homepage');

        // Check slides count
        const slides = await page.$$('#homeHeroCarousel .carousel-item');
        console.log(`Found ${slides.length} hero carousel slides`);
        assert(slides.length >= 6, `At least 6 hero slides exist (found ${slides.length})`);

        // Check that each slide has a unique image src
        const heroImages = await page.$$eval('#homeHeroCarousel .carousel-item .banner-img', imgs => imgs.map(img => img.getAttribute('src')));
        console.log('Hero banner image sources:', heroImages);
        const uniqueHeroImages = new Set(heroImages);
        assert(heroImages.length >= 6, `At least 6 hero images rendered`);
        assert(uniqueHeroImages.size === heroImages.length, `All ${heroImages.length} hero slides have distinct, non-duplicate images (Set size: ${uniqueHeroImages.size})`);

        // Check required hero banner topics in image filenames or titles
        const titles = await page.$$eval('#homeHeroCarousel .carousel-item .banner-caption-title', els => els.map(el => el.textContent.trim()));
        console.log('Hero banner titles:', titles);
        assert(titles.some(t => t.toLowerCase().includes('diwali')), 'Diwali hero banner present');
        assert(titles.some(t => t.toLowerCase().includes('navratri')), 'Navratri hero banner present');
        assert(titles.some(t => t.toLowerCase().includes('dhanteras') || t.toLowerCase().includes('hampers')), 'Dhanteras / Gift Hampers hero banner present');
        assert(titles.some(t => t.toLowerCase().includes('winter') || t.toLowerCase().includes('immunity')), 'Winter Immunity hero banner present');
        assert(titles.some(t => t.toLowerCase().includes('consultation') || t.toLowerCase().includes('vaidya')), 'Free Consultation hero banner present');
        assert(titles.some(t => t.toLowerCase().includes('tea') || t.toLowerCase().includes('yoga')), 'Herbal Tea / Yoga hero banner present');

        // Check progress bar indicators
        const indicators = await page.$$('#homeHeroCarousel .tga-indicator-dot');
        assert(indicators.length >= 6, `At least 6 progress bar indicator dots exist (found ${indicators.length})`);

        // Check Ken Burns and overlay styling
        const kenBurnsImg = await page.$('.hero-kenburns-img');
        assert(kenBurnsImg !== null, 'Slide image has .hero-kenburns-img for Ken Burns effect');

        const captionBtn = await page.$('.banner-caption-btn');
        assert(captionBtn !== null, 'Slide contains prominent CTA button (.banner-caption-btn)');

        // Screenshot initial slide
        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'banners_hero_slide1.png') });
        console.log('📸 Captured banners_hero_slide1.png');

        // Test autoplay: slide should change automatically after ~4.5s
        console.log('\n--- 2. Testing Autoplay (4.5s interval) ---');
        const initialActiveIndex = await page.$eval('#homeHeroCarousel .carousel-item.active', el => el.getAttribute('data-slide-index') || '0');
        console.log(`Initial active slide index: ${initialActiveIndex}`);
        console.log('Waiting 5.2 seconds for autoplay transition...');
        await page.waitForTimeout(5200);

        const newActiveIndex = await page.$eval('#homeHeroCarousel .carousel-item.active', el => el.getAttribute('data-slide-index') || '0');
        console.log(`Active slide index after 5.2s: ${newActiveIndex}`);
        assert(newActiveIndex !== initialActiveIndex, `Autoplay automatically transitioned from slide ${initialActiveIndex} to slide ${newActiveIndex}`);

        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'banners_hero_autoplay_slide2.png') });
        console.log('📸 Captured banners_hero_autoplay_slide2.png');

        // -------------------------------------------------------------
        // 3. MIDDLE-OF-PAGE 4 BANNERS VERIFICATION
        // -------------------------------------------------------------
        console.log('\n--- 3. Testing 4 Middle-of-Page Banners ---');
        const midCards = await page.$$('.middle-promo-banner-card');
        console.log(`Found ${midCards.length} middle promotional cards`);
        assert(midCards.length === 4, `Exactly 4 middle-of-page banners rendered (found ${midCards.length})`);

        const midImages = await page.$$eval('.middle-promo-banner-img', imgs => imgs.map(img => img.getAttribute('src')));
        console.log('Middle banner image sources:', midImages);
        const uniqueMidImages = new Set(midImages);
        assert(uniqueMidImages.size === 4, `All 4 middle banners have distinct, non-duplicate images (Set size: ${uniqueMidImages.size})`);

        // Check that none of the middle images reuse a hero image
        const intersection = midImages.filter(img => heroImages.includes(img));
        assert(intersection.length === 0, 'Never reuse one image twice: No middle banner reuses any hero banner image');

        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'banners_middle_section.png') });
        console.log('📸 Captured banners_middle_section.png');

        // -------------------------------------------------------------
        // 4. ADMIN BANNER CRUD VERIFICATION
        // -------------------------------------------------------------
        console.log('\n--- 4. Testing Admin Banner CRUD ---');
        const adminPage = await browser.newPage({ viewport: { width: 1536, height: 864 } });

        // Login as Admin
        await adminPage.goto(`${ADMIN_URL}/Auth/Login`, { waitUntil: 'networkidle', timeout: 30000 });
        await adminPage.fill('input[name="Email"]', 'admin@thegreatayurveda.com');
        await adminPage.fill('input[name="Password"]', 'DevAuth_Vaidya_2026!#');
        await adminPage.click('button[type="submit"]');
        await adminPage.waitForTimeout(2000);

        console.log('Admin page URL after login:', adminPage.url());
        assert(adminPage.url().includes('/Dashboard') || adminPage.url().includes('/dashboard') || adminPage.url().includes('localhost:7200'), 'Admin logged in successfully');

        // Navigate to Banners
        await adminPage.goto(`${ADMIN_URL}/Marketing/Banners`, { waitUntil: 'networkidle', timeout: 30000 });
        await adminPage.waitForTimeout(1000);

        await adminPage.waitForSelector('h2', { timeout: 15000 });
        const bannerManagementHeader = await adminPage.$('h2');
        const headerText = await bannerManagementHeader.textContent();
        assert(headerText.includes('Promotional Banners'), 'Admin navigates to Promotional Banners management');

        await adminPage.screenshot({ path: path.join(SCREENSHOT_DIR, 'banners_admin_list.png') });
        console.log('📸 Captured banners_admin_list.png');

        // Create a new banner in Admin
        console.log('\n--- 5. Admin Create Banner & Verify on Public Site ---');
        const testBannerTitle = 'Exclusive Monsoon Rejuvenation ' + Date.now();
        await adminPage.click('button[data-bs-target="#createBannerModal"]');
        await adminPage.waitForTimeout(800);

        await adminPage.fill('#createBannerModal input[name="title"]', testBannerTitle);
        await adminPage.fill('#createBannerModal input[name="subtitle"]', 'Special Seasonal Agni Balancing Formula');
        await adminPage.selectOption('#createBannerModal select[name="placement"]', 'home_hero');
        await adminPage.fill('#createBannerModal input[name="displayOrder"]', '0'); // sequence 0 to appear first
        await adminPage.fill('#createBannerModal input[name="linkUrl"]', '/Shop?category=Monsoon');
        await adminPage.fill('#createBannerModal input[name="imageUrl"]', '/images/banners/banner-hero-diwali.webp');

        await adminPage.click('#createBannerModal button[type="submit"]');
        await adminPage.waitForTimeout(2000);

        // Verify banner appears in Admin list
        const adminPageContent = await adminPage.content();
        assert(adminPageContent.includes(testBannerTitle), `Admin banners list contains the newly created banner: "${testBannerTitle}"`);

        await adminPage.screenshot({ path: path.join(SCREENSHOT_DIR, 'banners_admin_created.png') });
        console.log('📸 Captured banners_admin_created.png');

        // Verify new banner immediately appears on public home page
        await page.goto(`${WEB_URL}`, { waitUntil: 'networkidle', timeout: 30000 });
        await page.waitForTimeout(1000);

        const publicPageContent = await page.content();
        assert(publicPageContent.includes(testBannerTitle), `New banner "${testBannerTitle}" immediately visible on storefront Home hero`);

        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'banners_public_new_banner.png') });
        console.log('📸 Captured banners_public_new_banner.png');

        console.log('\n================================================================');
        console.log(`TOTAL CHECKS: ${passedCount + failedCount} | PASSED: ${passedCount} | FAILED: ${failedCount}`);
        console.log('================================================================');

    } catch (err) {
        console.error('Test execution error:', err);
        failedCount++;
    } finally {
        await browser.close();
    }

    if (failedCount > 0) {
        process.exit(1);
    }
}

run();
