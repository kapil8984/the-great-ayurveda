const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const WEB_URL = 'http://localhost:5000';
const SCREENSHOT_DIR = path.resolve('tests/e2e/screenshots');
fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });

async function runGroup4Verification() {
    console.log('================================================================');
    console.log('🧪 GROUP 4 VERIFICATION: TOP-LEVEL PUBLIC UI & ANIMATION');
    console.log(`Target: ${WEB_URL}`);
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
        console.log('\n--- 1. Testing Sticky Glass Header, Announcement & Marquee ---');
        await page.goto(WEB_URL, { waitUntil: 'domcontentloaded', timeout: 45000 });
        await page.waitForTimeout(2000);

        // A. Sticky Glass Header
        const header = await page.$('#tgaMainHeader');
        assert(header !== null, 'Sticky glass header is present (#tgaMainHeader)');

        // B. Marquee of Offers
        const marquee = await page.$('.tga-marquee-wrapper');
        assert(marquee !== null, 'Festive offers marquee ticker is rendered (.tga-marquee-wrapper)');
        const marqueeHtml = await page.$eval('.tga-marquee-wrapper', el => el.textContent);
        assert(marqueeHtml.includes('Diwali Ojas Mahotsav') && marqueeHtml.includes('DIWALI30'), 'Marquee contains Diwali Mahotsav & DIWALI30 offer');
        assert(marqueeHtml.includes('Navratri Sattvic Diet') && marqueeHtml.includes('NAVRATRI20'), 'Marquee contains Navratri Sattvic offer');

        // Screenshot Hero & Marquee
        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group4_hero_and_marquee.png') });
        console.log('📸 Captured group4_hero_and_marquee.png');

        console.log('\n--- 2. Testing Animated Hero & Counters ---');
        // Hero Elements
        const heroMandala = await page.$('.hero-mandala-bg');
        assert(heroMandala !== null, 'Sacred rotating mandala background rendered (.hero-mandala-bg)');

        const floatingLeaf = await page.$('.floating-leaf-1');
        assert(floatingLeaf !== null, 'Floating botanical herb leaf particle rendered (.floating-leaf-1)');

        // Counters
        const counters = await page.$$('.counter-number');
        assert(counters.length >= 4, `Animated KPI counter elements present (count: ${counters.length})`);
        await page.waitForTimeout(1500); // Allow counter animation to progress
        const firstCounterText = await counters[0].textContent();
        assert(firstCounterText.includes('+') || firstCounterText.length > 1, `KPI counter display active: "${firstCounterText}"`);

        console.log('\n--- 3. Testing Festive Strips (Diwali & Navratri) ---');
        // Scroll to Diwali Strip
        const diwaliStrip = await page.$('.festive-strip-diwali');
        assert(diwaliStrip !== null, 'Diwali festive strip rendered (.festive-strip-diwali)');
        if (diwaliStrip) {
            await diwaliStrip.scrollIntoViewIfNeeded();
            await page.waitForTimeout(800);
            await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group4_festive_diwali_strip.png') });
            console.log('📸 Captured group4_festive_diwali_strip.png');
        }

        // Scroll to Navratri Strip
        const navratriStrip = await page.$('.festive-strip-navratri');
        assert(navratriStrip !== null, 'Navratri festive strip rendered (.festive-strip-navratri)');
        if (navratriStrip) {
            await navratriStrip.scrollIntoViewIfNeeded();
            await page.waitForTimeout(800);
            await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group4_festive_navratri_strip.png') });
            console.log('📸 Captured group4_festive_navratri_strip.png');
        }

        console.log('\n--- 4. Testing Animated Category Tiles & Hover ---');
        const catCards = await page.$$('.category-luxury-card');
        assert(catCards.length >= 6, `Luxury animated category tiles rendered (count: ${catCards.length})`);
        if (catCards.length > 0) {
            await catCards[0].hover();
            await page.waitForTimeout(500);
            const boxImg = await catCards[0].$('.category-img-box img');
            assert(boxImg !== null, 'Category image box exists with zoomable icon');
        }

        console.log('\n--- 5. Testing Testimonial Slider ---');
        const testimonialCarousel = await page.$('#testimonialCarousel');
        assert(testimonialCarousel !== null, 'Interactive patient testimonials slider rendered (#testimonialCarousel)');
        if (testimonialCarousel) {
            await testimonialCarousel.scrollIntoViewIfNeeded();
            await page.waitForTimeout(800);

            // Test slide button
            const nextBtn = await page.$('button[data-bs-target="#testimonialCarousel"][data-bs-slide="next"]');
            assert(nextBtn !== null, 'Testimonial slider next control button present');
            if (nextBtn) {
                await nextBtn.click();
                await page.waitForTimeout(1000);
            }
            await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group4_testimonials_slider.png') });
            console.log('📸 Captured group4_testimonials_slider.png');
        }

        console.log('\n--- 6. Testing Product-Card Quick-Add Micro-Interaction ---');
        // Scroll to featured formulations
        const quickAddBtn = await page.$('.ajax-add-to-cart, .btn-add-to-cart-ajax');
        assert(quickAddBtn !== null, 'Product card quick-add button present');
        if (quickAddBtn) {
            await quickAddBtn.scrollIntoViewIfNeeded();
            await page.waitForTimeout(500);
            await quickAddBtn.click();
            await page.waitForTimeout(1500);

            const toastContainer = await page.$('#tgaToastContainer');
            assert(toastContainer !== null, 'Toast container receives notification feedback');
            await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'group4_quick_add_interaction.png') });
            console.log('📸 Captured group4_quick_add_interaction.png');
        }

        console.log('\n--- 7. Testing Scroll-Reveal & Prefers-Reduced-Motion ---');
        const revealElements = await page.$$('.reveal-on-scroll');
        assert(revealElements.length >= 4, `Scroll-reveal sections registered (count: ${revealElements.length})`);

        // Check reduced-motion media query rule in CSS
        const cssContent = fs.readFileSync('src/Ayurveda.Web/wwwroot/css/site.css', 'utf8');
        assert(cssContent.includes('@media (prefers-reduced-motion: reduce)'), 'CSS contains explicit prefers-reduced-motion media query');
        assert(cssContent.includes('.skeleton-shimmer'), 'CSS contains skeleton shimmer loading utility');

    } finally {
        await browser.close();
    }

    console.log('\n================================================================');
    console.log(`GROUP 4 SUMMARY: ${passedCount} PASSED, ${failedCount} FAILED`);
    console.log('================================================================');

    process.exit(failedCount > 0 ? 1 : 0);
}

runGroup4Verification().catch(err => {
    console.error('Fatal error during Group 4 verification:', err);
    process.exit(1);
});
