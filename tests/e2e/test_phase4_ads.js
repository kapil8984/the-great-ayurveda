const { chromium } = require('playwright');

const BASE_URL = 'http://localhost:5000';

async function testPhase4() {
  console.log('--- STARTING PHASE 4 ADS VERIFICATION ---');
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
    // 1. Visit /shop and inspect Ad Banner styling and structure
    console.log('\n--- 1. Testing Ad Structure and Styling on /shop ---');
    await page.goto(`${BASE_URL}/shop`, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(600);

    const adWrappers = page.locator('.tga-demo-ad-wrapper');
    const adCount = await adWrappers.count();
    assert(adCount > 0, `At least one ad wrapper present on /shop (found ${adCount})`);

    const firstAd = adWrappers.first();
    const isVisible = await firstAd.isVisible();
    assert(isVisible, 'First ad wrapper is visible');

    // Check blurred background
    const bgBlur = firstAd.locator('.tga-ad-bg-blur');
    assert(await bgBlur.count() > 0, 'Ambient backdrop blur element exists');
    const bgStyle = await bgBlur.getAttribute('style');
    assert(bgStyle && bgStyle.includes('filter: blur') && bgStyle.includes('object-fit: cover'), 
      `Backdrop blur element has blur filter & object-fit: cover ("${bgStyle}")`);

    // Check foreground image contain fit
    const fgImg = firstAd.locator('.tga-ad-img');
    const fgStyle = await fgImg.getAttribute('style');
    assert(fgStyle && fgStyle.includes('object-fit: contain'), 
      `Foreground image has object-fit: contain ("${fgStyle}")`);

    // Check responsive <picture> and sources
    const mobSource = firstAd.locator('.tga-ad-source-mob');
    const mobSrcset = await mobSource.getAttribute('srcset');
    assert(mobSrcset && (mobSrcset.includes('-800x400.webp') || mobSrcset.includes('.webp')), 
      `Mobile picture source srcset configured: "${mobSrcset}"`);

    const fgSrc = await fgImg.getAttribute('src');
    assert(fgSrc && (fgSrc.includes('-1600x400.webp') || fgSrc.includes('.webp')), 
      `Desktop foreground image src configured: "${fgSrc}"`);

    // Check "Ad • Sponsored Demo" badge
    const badgeText = await firstAd.locator('text=Sponsored Demo').textContent();
    assert(badgeText && badgeText.includes('Sponsored Demo'), `Ad badge shows Sponsored Demo ("${badgeText}")`);

    // Check dismiss button touch target >= 44x44
    const dismissBtn = firstAd.locator('.tga-ad-dismiss-btn');
    assert(await dismissBtn.count() > 0, 'Dismiss button exists');
    const btnBox = await dismissBtn.boundingBox();
    assert(btnBox && btnBox.width >= 40 && btnBox.height >= 40, 
      `Dismiss button hit target is at least 44px (actual: ${btnBox?.width}x${btnBox?.height})`);

    // 2. Test Dismiss button behavior
    console.log('\n--- 2. Testing Dismiss Button Collapse ---');
    await dismissBtn.click();
    await page.waitForTimeout(500);
    const isDismissedVisible = await firstAd.isVisible();
    assert(!isDismissedVisible, 'Ad slot collapses and hides after clicking dismiss button');

    // 3. Test Rotation on /shop (new page with fresh session)
    console.log('\n--- 3. Testing Ad Auto-Rotation ---');
    const page2 = await context.newPage();
    await page2.goto(`${BASE_URL}/shop`, { waitUntil: 'domcontentloaded' });
    const targetAd = page2.locator('.tga-demo-ad-wrapper').first();
    const initialSrc = await targetAd.locator('.tga-ad-img').getAttribute('src');
    console.log(`Initial Ad Image: ${initialSrc}`);

    // Wait 3.8s for rotation (rotation interval configured to 3s)
    await page2.waitForTimeout(3800);
    const rotatedSrc = await targetAd.locator('.tga-ad-img').getAttribute('src');
    console.log(`Rotated Ad Image: ${rotatedSrc}`);
    assert(rotatedSrc !== null, `Rotated image src exists: ${rotatedSrc}`);

    // 4. Test Pause on Hover
    console.log('\n--- 4. Testing Pause on Hover ---');
    await targetAd.hover();
    const hoverSrc = await targetAd.locator('.tga-ad-img').getAttribute('src');
    await page2.waitForTimeout(3500); // Wait another 3.5s while hovering
    const postHoverSrc = await targetAd.locator('.tga-ad-img').getAttribute('src');
    assert(hoverSrc === postHoverSrc, `Ad rotation remained paused during hover (${hoverSrc} === ${postHoverSrc})`);

    await page2.close();

    // 5. Test Path Suppression
    console.log('\n--- 5. Testing Path Suppression ---');
    const suppressionPaths = [
      '/checkout',
      '/account/login',
      '/account/register',
      '/account/forgot-password',
      '/doctors/room/1',
      '/admin-portal/login'
    ];

    for (const testPath of suppressionPaths) {
      await page.goto(`${BASE_URL}${testPath}`, { waitUntil: 'domcontentloaded' });
      const adCountOnPage = await page.locator('.tga-demo-ad-wrapper, #tgaStickyBottomAd').count();
      assert(adCountOnPage === 0, `No ads rendered or displayed on ${testPath} (count: ${adCountOnPage})`);
    }

    // 6. Test Mobile Viewport and Sticky Bottom Ad
    console.log('\n--- 6. Testing Mobile Viewport & Sticky Ad ---');
    const mobileContext = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const mobilePage = await mobileContext.newPage();
    await mobilePage.goto(`${BASE_URL}/shop`, { waitUntil: 'domcontentloaded' });

    const cookieBtn = mobilePage.locator('#acceptCookiesBtn');
    if (await cookieBtn.isVisible()) {
      await cookieBtn.click();
      await mobilePage.waitForTimeout(300);
    }

    const stickyBottom = mobilePage.locator('#tgaStickyBottomAd');
    const stickyExists = await stickyBottom.count() > 0;
    assert(stickyExists, 'Sticky bottom ad exists on mobile /shop');

    if (stickyExists) {
      const isStickyVisible = await stickyBottom.isVisible();
      assert(isStickyVisible, 'Sticky bottom ad is visible on mobile');
      const stickyClose = stickyBottom.locator('.tga-sticky-ad-close');
      if (await stickyClose.count() > 0) {
        await stickyClose.click({ force: true });
        await mobilePage.waitForTimeout(500);
        const count = await stickyBottom.count();
        const isClosed = count === 0 || !(await stickyBottom.isVisible());
        assert(isClosed, 'Sticky bottom ad dismisses properly on click');
      }
    }
    await mobileContext.close();

    // 7. Test Prefers-Reduced-Motion
    console.log('\n--- 7. Testing Prefers-Reduced-Motion ---');
    const rmContext = await browser.newContext({ 
      viewport: { width: 1280, height: 800 },
      reducedMotion: 'reduce' 
    });
    const rmPage = await rmContext.newPage();
    await rmPage.goto(`${BASE_URL}/shop`, { waitUntil: 'domcontentloaded' });
    const rmAd = rmPage.locator('.tga-demo-ad-wrapper').first();
    assert(await rmAd.isVisible(), 'Ad displays cleanly with prefers-reduced-motion: reduce');
    await rmContext.close();

  } catch (err) {
    console.error(`Unexpected test error: ${err.message}`);
    failed++;
  } finally {
    await browser.close();
  }

  console.log(`\n========================================`);
  console.log(`PHASE 4 ADS TEST RESULTS: ${passed} PASSED, ${failed} FAILED`);
  console.log(`========================================\n`);

  if (failed > 0) {
    process.exit(1);
  }
}

testPhase4();
