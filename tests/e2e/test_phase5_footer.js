const { chromium } = require('playwright');

const BASE_URL = 'http://localhost:5000';

async function testPhase5() {
  console.log('--- STARTING PHASE 5 FOOTER & ABOUT PROJECT VERIFICATION ---');
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  const page = await context.newPage();

  let passed = 0;
  let failed = 0;

  function assert(condition, message) {
    if (condition) {
      console.log(`[PASS] ${message}`);
      passed++;
    } else {
      console.error(`[FAIL] ${message}`);
      failed++;
    }
  }

  try {
    // 1. Visit / and verify old disclaimer is gone and new link is present
    console.log('\n--- 1. Testing Home Page Footer ---');
    await page.goto(`${BASE_URL}/`, { waitUntil: 'domcontentloaded' });
    const homeHtml = await page.content();

    assert(!homeHtml.includes('Demo website, sample data. AI-generated sample images.'), 
      'Home page does NOT contain old disclaimer string');
    assert(!homeHtml.includes('AI-generated sample images'), 
      'Home page does NOT contain "AI-generated sample images"');
    assert(homeHtml.includes('About this project'), 
      'Home page contains "About this project" link text');
    assert(homeHtml.includes('/about-project'), 
      'Home page links to "/about-project"');

    // 2. Visit /shop and verify footer
    console.log('\n--- 2. Testing Shop Page Footer ---');
    await page.goto(`${BASE_URL}/shop`, { waitUntil: 'domcontentloaded' });
    const shopHtml = await page.content();

    assert(!shopHtml.includes('Demo website, sample data. AI-generated sample images.'), 
      'Shop page does NOT contain old disclaimer string');
    assert(!shopHtml.includes('AI-generated sample images'), 
      'Shop page does NOT contain "AI-generated sample images"');
    assert(shopHtml.includes('About this project'), 
      'Shop page contains "About this project" link');

    // 3. Test /about-project Page
    console.log('\n--- 3. Testing /about-project Page ---');
    const resp = await page.goto(`${BASE_URL}/about-project`, { waitUntil: 'domcontentloaded' });
    assert(resp.status() === 200, `GET /about-project returned status 200 (actual: ${resp.status()})`);

    const title = await page.title();
    assert(title.includes('About This Project'), `Page title contains "About This Project": "${title}"`);

    const pageHeading = await page.locator('h1').textContent();
    assert(pageHeading.includes('About The Great Ayurveda'), `Heading is "${pageHeading.trim()}"`);

    const bodyText = await page.locator('.tga-about-project-page').textContent();
    assert(bodyText.includes('Clean Architecture'), 'Mentions Clean Architecture');
    assert(bodyText.includes('WebRTC Video Consultations'), 'Mentions WebRTC Video Consultations');
    assert(bodyText.includes('Demonstration Data Notice'), 'Contains Demonstration Data Notice');
    assert(bodyText.includes('sample fictional profiles'), 'Explains fictional doctor profiles');
    assert(bodyText.includes('sandboxed test mode'), 'Explains test payment sandbox');

    // Check no horizontal scrollbar on mobile
    console.log('\n--- 4. Testing /about-project on Mobile (390x844) ---');
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(`${BASE_URL}/about-project`, { waitUntil: 'domcontentloaded' });
    const scrollWidth = await page.evaluate(() => document.documentElement.scrollWidth);
    const clientWidth = await page.evaluate(() => document.documentElement.clientWidth);
    assert(scrollWidth <= clientWidth, `No horizontal overflow on mobile (${scrollWidth} <= ${clientWidth})`);

    // 5. Verify preserved strings
    console.log('\n--- 5. Verifying Preserved Strings ---');
    // A. "Sample doctor profile (fictional)" on /doctors
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.goto(`${BASE_URL}/doctors`, { waitUntil: 'domcontentloaded' });
    const docHtml = await page.content();
    assert(docHtml.includes('Sample doctor profile (fictional)'), 
      'Preserved "Sample doctor profile (fictional)" on /doctors');

    // B. "Sponsored - Demo" or "Sponsored Demo" on ads
    assert(shopHtml.includes('Sponsored Demo') || shopHtml.includes('Sponsored - Demo'), 
      'Preserved "Sponsored Demo" / "Sponsored - Demo"');

  } catch (err) {
    console.error(`Unexpected test error: ${err.message}`);
    failed++;
  } finally {
    await browser.close();
  }

  console.log(`\n========================================`);
  console.log(`PHASE 5 FOOTER TEST RESULTS: ${passed} PASSED, ${failed} FAILED`);
  console.log(`========================================\n`);

  if (failed > 0) {
    process.exit(1);
  }
}

testPhase5();
