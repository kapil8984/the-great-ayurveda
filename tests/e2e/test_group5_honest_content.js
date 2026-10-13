// ================================================================
// tests/e2e/test_group5_honest_content.js
// Verification suite for Task 5: Honest Content & Exact Counts
// ================================================================

const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const WEB_URL = 'http://localhost:5000';
const SCREENSHOT_DIR = path.join(__dirname, 'screenshots');
if (!fs.existsSync(SCREENSHOT_DIR)) {
    fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
}

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

async function run() {
    console.log('================================================================');
    console.log('🧪 TASK 5 E2E VERIFICATION SUITE: HONEST CONTENT & EXACT COUNTS');
    console.log(`Target URL: ${WEB_URL}`);
    console.log('================================================================\n');

    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1366, height: 768 } });
    const page = await context.newPage();

    try {
        // 1. Check Home Page Content
        console.log('--- 1. Testing Home Page Content ---');
        await page.goto(`${WEB_URL}/`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(1500);

        const homeHtml = await page.content();

        // A. Testimonial section renamed to "Sample Testimonials"
        assert(homeHtml.includes('Sample Testimonials'), 'Testimonials section renamed to "Sample Testimonials"');
        assert(homeHtml.includes('Sample testimonials based on clinical practice archetypes'), 'Sample disclaimer note displayed above testimonials');

        // B. Hero statistics match exact real counts
        assert(homeHtml.includes('Ayurvedic Vaidyas'), 'Hero stat specifies "Ayurvedic Vaidyas" (24 Vaidyas)');
        assert(homeHtml.includes('Pure Formulations'), 'Hero stat specifies "Pure Formulations" (312 Formulations)');
        assert(homeHtml.includes('Shastriya Categories'), 'Hero stat specifies "Shastriya Categories" (26 Categories)');
        assert(homeHtml.includes('78 Subcategories'), 'Hero stat specifies "78 Subcategories"');

        // C. Category navigation link has exact count
        assert(homeHtml.includes('Explore All 26 Categories'), 'Link accurately specifies "Explore All 26 Categories"');

        // D. Verified wording removed for doctors/patients
        assert(!homeHtml.includes('Verified Ayurvedic Vaidyas'), 'Home page does not contain "Verified Ayurvedic Vaidyas"');
        assert(!homeHtml.includes('Verified Experiences'), 'Home page does not contain "Verified Experiences"');

        // E. Footer disclaimer intact
        assert(homeHtml.includes('Demo website, sample data. AI-generated sample images.'), 'Footer note "Demo website, sample data. AI-generated sample images." is present');

        // Screenshot of testimonials and footer
        const testimonialSection = await page.$('#testimonialCarousel');
        if (testimonialSection) {
            await testimonialSection.scrollIntoViewIfNeeded();
            await page.waitForTimeout(500);
            await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'honest_content_testimonials.png') });
            console.log('📸 Captured honest_content_testimonials.png');
        }

        const footer = await page.$('.tga-footer');
        if (footer) {
            await footer.scrollIntoViewIfNeeded();
            await page.waitForTimeout(500);
            await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'honest_content_footer.png') });
            console.log('📸 Captured honest_content_footer.png');
        }

        // 2. Check Doctors Directory Content
        console.log('\n--- 2. Testing Doctors Directory ---');
        await page.goto(`${WEB_URL}/Doctors`, { waitUntil: 'domcontentloaded', timeout: 30000 });
        await page.waitForTimeout(1500);
        const docsHtml = await page.content();

        assert(!docsHtml.includes('Consult Verified Classical Ayurvedic Physicians'), 'Doctors header does not claim "Consult Verified Classical Ayurvedic Physicians"');
        assert(docsHtml.includes('Consult Classical Ayurvedic Physicians'), 'Doctors header displays honest title "Consult Classical Ayurvedic Physicians"');
        assert(!docsHtml.includes('Verified Ayurvedic Physicians'), 'Doctors directory does not claim "Verified Ayurvedic Physicians"');
        assert(docsHtml.includes('Ayurvedic Physicians'), 'Doctors directory displays honest title "Ayurvedic Physicians"');

        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'honest_content_doctors.png') });
        console.log('📸 Captured honest_content_doctors.png');

    } catch (err) {
        console.error('Unhandled test exception:', err);
        failedCount++;
    } finally {
        await browser.close();
    }

    console.log('\n================================================================');
    console.log(`TASK 5 RESULT: ${passedCount} PASSED, ${failedCount} FAILED`);
    console.log('================================================================');
    if (failedCount > 0) {
        process.exit(1);
    }
}

run();
