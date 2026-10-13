const { chromium } = require('playwright');
(async () => {
    const browser = await chromium.launch({ headless: true });
    const page = await browser.newPage();
    await page.goto('http://localhost:5000/Shop', { waitUntil: 'domcontentloaded' });
    const link = await page.$('a[href*="/Shop/Details/"], a[href*="/Details/"]');
    const href = await link.getAttribute('href');
    console.log('Navigating to', href);
    await page.goto('http://localhost:5000' + href, { waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(2000);
    const broken = await page.evaluate(() => {
        return Array.from(document.querySelectorAll('img'))
            .filter(img => !img.complete || img.naturalWidth === 0)
            .map(img => ({ src: img.src, alt: img.alt, outer: img.outerHTML }));
    });
    console.log('Broken images count:', broken.length);
    console.log('Broken images:', JSON.stringify(broken, null, 2));

    const html = await page.content();
    console.log('Contains traditionally used for?:', html.toLowerCase().includes('traditionally used for'));
    
    await browser.close();
})();
