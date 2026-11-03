const { chromium } = require('playwright');
(async () => {
    const browser = await chromium.launch();
    const page = await browser.newPage();
    
    console.log("Testing Doctor Login...");
    await page.goto('http://localhost:5000/doctor/login');
    await page.fill('#loginForm input[name="Email"]', 'demo.doctor@thegreatayurveda.com');
    await page.fill('#loginForm input[name="Password"]', 'Doctor@123');
    await Promise.all([
        page.waitForNavigation(),
        page.click('#loginForm button[type="submit"]')
    ]);
    console.log('Doctor login result URL:', page.url());
    const docErrors = await page.$$eval('.alert, .validation-summary-errors, .field-validation-error, .text-danger', els => els.map(e => e.innerText).filter(t => t.trim().length > 0));
    console.log('Doctor errors:', docErrors);
    
    console.log("\nTesting Admin Login...");
    await page.goto('http://localhost:5000/admin-portal/auth/login');
    await page.fill('#Email', 'admin@thegreatayurveda.com');
    await page.fill('#Password', 'SuperAdmin@2026!');
    await Promise.all([
        page.waitForNavigation(),
        page.click('#submitBtn')
    ]);
    console.log('Admin login result URL:', page.url());
    const admErrors = await page.$$eval('.alert, .validation-summary-errors, .field-validation-error, .text-danger', els => els.map(e => e.innerText).filter(t => t.trim().length > 0));
    console.log('Admin errors:', admErrors);
    
    await browser.close();
})();
