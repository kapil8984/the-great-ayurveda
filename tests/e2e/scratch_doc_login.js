const { chromium } = require('playwright');
(async () => {
    const browser = await chromium.launch();
    const page = await browser.newPage();
    await page.goto('http://localhost:5000/doctor/login');
    console.log('After goto doctor/login, URL is:', page.url());
    await page.fill('#loginForm input[name="Email"]', 'demo.doctor@thegreatayurveda.com');
    await page.fill('#loginForm input[name="Password"]', 'Doctor@123');
    await Promise.all([
        page.waitForNavigation(),
        page.click('#loginForm button[type="submit"]')
    ]);
    console.log('After submit, URL is:', page.url());
    const errors = await page.$$eval('.validation-summary-errors, .field-validation-error, .text-danger, .alert-danger', els => els.map(e => e.innerText));
    console.log('Errors:', errors);
    await browser.close();
})();
