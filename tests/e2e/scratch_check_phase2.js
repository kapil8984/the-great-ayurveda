const { chromium } = require('playwright');
const fs = require('fs');
const path = require('path');

const BASE_URL = 'http://localhost:5000';

async function run() {
    const browser = await chromium.launch({ headless: true });

    // 1. Check Verify OTP
    console.log('1. Checking Verify OTP at 320px...');
    const p1 = await browser.newPage({ viewport: { width: 320, height: 640 } });
    await p1.goto(`${BASE_URL}/account/verify-otp?email=test%40example.com`, { waitUntil: 'networkidle' });
    const sWidth = await p1.evaluate(() => document.documentElement.scrollWidth);
    const cWidth = await p1.evaluate(() => document.documentElement.clientWidth);
    console.log(`VerifyOtp @ 320px: scrollWidth=${sWidth}, clientWidth=${cWidth}`);

    await p1.click('#verifyOtpSubmitBtn');
    await p1.waitForTimeout(300);
    const errText = await p1.evaluate(() => {
        const el = document.getElementById('clientOtpError');
        return el ? { text: el.textContent, isVisible: !el.classList.contains('d-none') } : null;
    });
    console.log('Client OTP error result:', errText);
    await p1.close();

    // 2. Login as customer and check Call Room & Orders
    console.log('2. Logging in as customer...');
    const context = await browser.newContext({ viewport: { width: 390, height: 844 } });
    const page = await context.newPage();
    await page.goto(`${BASE_URL}/account/login`, { waitUntil: 'networkidle' });
    await page.fill('input[name="Email"]', 'customer@thegreatayurveda.com');
    await page.fill('input[name="Password"]', 'Customer@123');
    await page.click('.card form button[type="submit"]');
    await page.waitForTimeout(1000);
    console.log('Current URL after login:', page.url());

    // Check Call room
    console.log('3. Checking Call Room...');
    await page.goto(`${BASE_URL}/doctors/room/1`, { waitUntil: 'networkidle' });
    console.log('Call Room Title:', await page.title());
    const remoteTitle = await page.evaluate(() => document.getElementById('remoteParticipantTitle')?.innerText || '');
    console.log('Remote Participant Title:', remoteTitle);

    // Enter call
    const joinBtn = await page.$('#joinRoomBtn');
    if (joinBtn) {
        console.log('Clicking Join Consultation Now...');
        await joinBtn.click();
        await page.waitForTimeout(600);
    }
    const isCallVisible = await page.evaluate(() => {
        const cs = document.getElementById('callSection');
        return cs && !cs.classList.contains('d-none');
    });
    console.log('In-call section visible:', isCallVisible);

    const isToggleChatVisible = await page.evaluate(() => {
        const btn = document.getElementById('callToggleChatBtn');
        return btn && btn.offsetParent !== null;
    });
    console.log('Chat toggle button visible:', isToggleChatVisible);

    if (isToggleChatVisible) {
        await page.click('#callToggleChatBtn');
        await page.waitForTimeout(400);
        const isSheetOpen = await page.evaluate(() => {
            const col = document.getElementById('callSidePanelColumn');
            return col && col.classList.contains('sheet-open');
        });
        console.log('Chat bottom sheet open:', isSheetOpen);
    }

    // Check Orders list to find a valid order id
    console.log('4. Checking Orders...');
    await page.goto(`${BASE_URL}/Orders`, { waitUntil: 'networkidle' });
    const orderLink = await page.$('a[href*="/Orders/Details/"]');
    let orderUrl = `${BASE_URL}/Orders/Details/1`;
    if (orderLink) {
        const href = await orderLink.getAttribute('href');
        orderUrl = `${BASE_URL}${href}`;
        console.log('Found user order URL:', orderUrl);
    } else {
        console.log('No order link found on /Orders, testing /Orders/Details/1 directly');
    }

    await page.goto(orderUrl, { waitUntil: 'networkidle' });
    console.log('Order Details URL:', page.url());
    const mapStatus = await page.evaluate(() => document.getElementById('mapStatusText')?.textContent || '');
    console.log('Map status text:', mapStatus);
    const hasRawBullet = mapStatus.includes('&bull;');
    console.log('Has raw &bull;:', hasRawBullet);

    const actionsGrid = await page.evaluate(() => {
        const el = document.querySelector('.tga-order-actions-grid');
        if (!el) return null;
        const cs = window.getComputedStyle(el);
        return { display: cs.display, gridTemplateColumns: cs.gridTemplateColumns };
    });
    console.log('Actions grid:', actionsGrid);

    // 5. Checkout Confirmation
    console.log('5. Checking Checkout Confirmation...');
    await page.goto(`${BASE_URL}/checkout/confirmation/1`, { waitUntil: 'networkidle' });
    const dlInfo = await page.evaluate(() => {
        const dl = document.querySelector('dl');
        return dl ? { overflowWrap: window.getComputedStyle(dl).overflowWrap } : null;
    });
    console.log('DL overflow-wrap:', dlInfo);

    const badgePos = await page.evaluate(() => {
        const b = document.querySelector('.badge.bg-warning-subtle');
        return b ? window.getComputedStyle(b).position : null;
    });
    console.log('Badge position:', badgePos);

    await browser.close();
    console.log('=== CHECK COMPLETE ===');
}

run().catch(console.error);
