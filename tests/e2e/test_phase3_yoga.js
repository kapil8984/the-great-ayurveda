const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE_URL = 'http://localhost:5000';

async function testPhase3() {
  console.log('--- STARTING PHASE 3 YOGA VERIFICATION ---');
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
    // 1. Visit /yoga
    await page.goto(`${BASE_URL}/yoga`, { waitUntil: 'networkidle' });
    const title = await page.title();
    assert(title.includes('12 Classical Yoga Asanas'), `Page title contains 12 Classical Yoga: "${title}"`);

    // 2. Check total cards
    const cards = page.locator('#yogaCardsGrid .tga-yoga-card');
    const totalCount = await cards.count();
    assert(totalCount === 12, `Total cards in DOM is 12 (actual: ${totalCount})`);

    // 3. Check initial visibility: exactly 4 visible, 8 extra
    const visibleCards = page.locator('#yogaCardsGrid > div:not(.d-none)');
    const initialVisibleCount = await visibleCards.count();
    assert(initialVisibleCount === 4, `Initial visible cards is 4 (actual: ${initialVisibleCount})`);

    const hiddenCards = page.locator('#yogaCardsGrid > div.tga-yoga-extra.d-none');
    const hiddenCount = await hiddenCards.count();
    assert(hiddenCount === 8, `Initial hidden extra cards is 8 (actual: ${hiddenCount})`);

    // 4. Test "Show 8 more" toggle
    const toggleBtn = page.locator('#yogaToggleMoreBtn');
    assert(await toggleBtn.isVisible(), 'Show 8 more button is visible');
    const toggleText = await page.locator('#yogaToggleText').textContent();
    assert(toggleText.includes('Show 8 more'), `Toggle button text is "${toggleText}"`);

    await toggleBtn.click();
    await page.waitForTimeout(400);

    const expandedVisibleCount = await page.locator('#yogaCardsGrid > div:not(.d-none)').count();
    assert(expandedVisibleCount === 12, `After clicking toggle, all 12 cards are visible (actual: ${expandedVisibleCount})`);

    const textAfterClick = await page.locator('#yogaToggleText').textContent();
    assert(textAfterClick.includes('Show less'), `Toggle button text changed to "Show less" (actual: "${textAfterClick}")`);

    // Toggle back to collapse
    await toggleBtn.click();
    await page.waitForTimeout(400);
    const reCollapsedCount = await page.locator('#yogaCardsGrid > div:not(.d-none)').count();
    assert(reCollapsedCount === 4, `After clicking "Show less", cards re-collapsed to 4 (actual: ${reCollapsedCount})`);

    // Expand again for inspecting cards
    await toggleBtn.click();
    await page.waitForTimeout(400);

    // 5. Inspect card content across all 12 asanas
    for (let i = 0; i < 12; i++) {
      const card = cards.nth(i);
      const sanskrit = (await card.locator('.font-serif').textContent()).trim();
      const english = (await card.locator('.text-muted.small.fw-medium').textContent()).trim();
      const diff = (await card.locator('.tga-yoga-badges .badge').first().textContent()).trim();
      const benefits = await card.locator('ul li').count();
      const img = card.locator('.tga-yoga-img');
      const imgSrc = await img.getAttribute('src');

      assert(sanskrit.length > 0 && english.length > 0, `Card ${i + 1}: Names present: "${sanskrit}" / "${english}"`);
      assert(imgSrc && imgSrc.endsWith('.webp'), `Card ${i + 1}: Image is WebP (${imgSrc})`);
      assert(benefits >= 1, `Card ${i + 1}: Contains traditionally practiced for list (${benefits} items)`);

      // Verify file size on disk is < 40KB
      const localRelPath = imgSrc.replace(/^\//, '').replace(/\//g, path.sep);
      const fullPath = path.resolve(__dirname, '../../src/Ayurveda.Web/wwwroot', localRelPath);
      if (fs.existsSync(fullPath)) {
        const stats = fs.statSync(fullPath);
        const kb = stats.size / 1024;
        assert(kb < 40, `Card ${i + 1}: Image file size is ${kb.toFixed(1)} KB (< 40 KB)`);
      }
    }

    // 6. Test YouTube nocookie facade demonstration on index
    const firstDemoBtn = page.locator('.tga-facade-trigger').first();
    assert(await firstDemoBtn.isVisible(), 'Demonstration button is visible on card');
    await firstDemoBtn.click();
    await page.waitForTimeout(500);

    const modal = page.locator('#yogaVideoModal');
    assert(await modal.isVisible(), 'YouTube demonstration modal opened');

    const iframe = page.locator('#yogaVideoContainer iframe');
    const iframeSrc = await iframe.getAttribute('src');
    assert(iframeSrc.includes('https://www.youtube-nocookie.com/embed/'), `Iframe loads from youtube-nocookie.com (${iframeSrc})`);

    // Close modal
    await page.locator('#yogaVideoModal .btn-close').click();
    await page.waitForTimeout(400);
    const containerHtml = await page.locator('#yogaVideoContainer').innerHTML();
    assert(containerHtml.trim() === '', 'Video iframe removed on modal close to cease audio/network');

    // 7. Test /yoga/details/1
    await page.goto(`${BASE_URL}/yoga/details/1`, { waitUntil: 'networkidle' });
    const detailsH1 = (await page.locator('h1.font-serif').textContent()).trim();
    assert(detailsH1.includes('Surya Namaskar'), `Details page H1 is Sanskrit name: "${detailsH1}"`);

    // Check steps grid
    const stepCards = page.locator('.tga-step-card');
    const stepsCount = await stepCards.count();
    assert(stepsCount >= 5, `Details page renders 5-6 steps (actual: ${stepsCount})`);

    for (let s = 0; s < stepsCount; s++) {
      const stepImg = stepCards.nth(s).locator('img');
      const stepImgSrc = await stepImg.getAttribute('src');
      assert(stepImgSrc && stepImgSrc.endsWith('.webp'), `Step ${s + 1} has WebP image (${stepImgSrc})`);
      
      const localRelPath = stepImgSrc.replace(/^\//, '').replace(/\//g, path.sep);
      const fullPath = path.resolve(__dirname, '../../src/Ayurveda.Web/wwwroot', localRelPath);
      if (fs.existsSync(fullPath)) {
        const stats = fs.statSync(fullPath);
        const kb = stats.size / 1024;
        assert(kb < 40, `Step ${s + 1} image size is ${kb.toFixed(1)} KB (< 40 KB)`);
      }
    }

    // Check details YouTube facade
    const detailsFacade = page.locator('#detailsVideoFacade');
    assert(await detailsFacade.isVisible(), 'Details YouTube facade is visible');
    await detailsFacade.click();
    await page.waitForTimeout(500);
    const detailsIframe = page.locator('#detailsVideoFacade iframe');
    const detailsIframeSrc = await detailsIframe.getAttribute('src');
    assert(detailsIframeSrc.includes('https://www.youtube-nocookie.com/embed/1ZpMUz1rfcA'), `Details iframe loaded from youtube-nocookie.com (${detailsIframeSrc})`);

    // 8. Mobile viewport check on /yoga/details/1
    await page.setViewportSize({ width: 390, height: 844 });
    await page.waitForTimeout(300);
    const scrollContainer = page.locator('.tga-steps-scroll-container');
    const overflowX = await scrollContainer.evaluate(el => window.getComputedStyle(el).overflowX);
    assert(overflowX === 'auto' || overflowX === 'scroll', `Mobile steps container has horizontal scroll (${overflowX})`);

    // 9. Admin Yoga Edit Form Check
    console.log('Testing Admin Yoga Edit form...');
    await page.setViewportSize({ width: 1280, height: 800 });
    // Log in as Admin
    await page.goto(`${BASE_URL}/admin-portal/auth/login`, { waitUntil: 'networkidle' });
    const { getAdminPassword } = require('./getTestPassword');
    await page.locator('input[name="Email"]').fill('admin@thegreatayurveda.com');
    await page.locator('input[name="Password"]').fill(getAdminPassword());
    await page.locator('button[type="submit"]').click();
    await page.waitForTimeout(1000);

    await page.goto(`${BASE_URL}/admin-portal/yoga`, { waitUntil: 'networkidle' });
    const adminRows = page.locator('table tbody tr');
    const adminRowCount = await adminRows.count();
    assert(adminRowCount === 12, `Admin Yoga directory displays all 12 asanas (actual: ${adminRowCount})`);

    // Click Edit on first row
    const editBtn = adminRows.first().locator('a:has-text("Edit")');
    assert(await editBtn.isVisible(), 'Edit button exists in admin table row');
    await editBtn.click();
    await page.waitForTimeout(500);

    const editH2 = (await page.locator('h2').textContent()).trim();
    assert(editH2.includes('Edit Yoga Posture'), `Admin Edit page loaded: "${editH2}"`);

    const nameInput = page.locator('input[name="YogaName"]');
    const currentName = await nameInput.inputValue();
    assert(currentName.length > 0, `Name input has current value: "${currentName}"`);

    const instructionsText = await page.locator('textarea[name="Instructions"]').inputValue();
    assert(instructionsText.length > 20, `Instructions textarea populated (${instructionsText.length} chars)`);

    const benefitsText = await page.locator('textarea[name="BenefitsJson"]').inputValue();
    assert(benefitsText.length > 20, `BenefitsJson textarea populated (${benefitsText.length} chars)`);

    // Test form submission (save without crashing or data loss)
    await page.locator('button[type="submit"]:has-text("Save Changes")').click();
    await page.waitForTimeout(1000);

    const redirectedUrl = page.url();
    assert(redirectedUrl.includes('/admin-portal/yoga') || redirectedUrl.includes('/admin/yoga'), `Admin saved successfully and redirected to index: ${redirectedUrl}`);

  } catch (err) {
    console.error('UNEXPECTED TEST ERROR:', err);
    failed++;
  } finally {
    await browser.close();
    console.log(`\n--- PHASE 3 RESULTS: ${passed} PASSED, ${failed} FAILED ---`);
    if (failed > 0) process.exit(1);
  }
}

testPhase3();
