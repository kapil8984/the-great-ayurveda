const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');
const sharp = require('sharp');

const BASE_URL = 'http://localhost:5000';
const BASELINE_DIR = path.join(__dirname, 'screenshots', 'baseline');
const CURRENT_DIR = path.join(__dirname, 'screenshots', 'current');

async function checkAdminDiff() {
  const browser = await chromium.launch({ headless: true });
  const adminContext = await browser.newContext({
    viewport: { width: 1366, height: 768 },
    deviceScaleFactor: 1
  });
  const adminPage = await adminContext.newPage();
  await adminPage.goto(`${BASE_URL}/admin-portal/auth/login`, { waitUntil: 'domcontentloaded' });
  await adminPage.fill('input[name="Email"], #adminEmail', 'admin@thegreatayurveda.com');
  await adminPage.fill('input[name="Password"], #adminPassword', 'Admin@123');
  await Promise.all([
    adminPage.waitForNavigation({ waitUntil: 'domcontentloaded' }).catch(() => {}),
    adminPage.click('button[type="submit"]')
  ]);

  // 1. Admin Dashboard
  await adminPage.goto(`${BASE_URL}/admin-portal/dashboard`, { waitUntil: 'domcontentloaded' });
  await adminPage.waitForTimeout(500);
  const currentDash = path.join(CURRENT_DIR, 'admin_dashboard_1366x768.png');
  await adminPage.screenshot({ path: currentDash });

  // 2. Admin Products
  await adminPage.goto(`${BASE_URL}/admin-portal/products`, { waitUntil: 'domcontentloaded' });
  await adminPage.waitForTimeout(500);
  const currentProd = path.join(CURRENT_DIR, 'admin_products_1366x768.png');
  await adminPage.screenshot({ path: currentProd });

  await browser.close();

  // Compare dashboard
  const baseDash = path.join(BASELINE_DIR, 'admin_dashboard_1366x768.png');
  if (fs.existsSync(baseDash)) {
    const bImg = sharp(baseDash);
    const cImg = sharp(currentDash);
    const bMeta = await bImg.metadata();
    const bBuf = await bImg.raw().toBuffer();
    const cBuf = await cImg.raw().toBuffer();
    let diff = 0;
    for (let i = 0; i < bBuf.length; i += 4) {
      if (Math.abs(bBuf[i]-cBuf[i]) + Math.abs(bBuf[i+1]-cBuf[i+1]) + Math.abs(bBuf[i+2]-cBuf[i+2]) > 40) diff++;
    }
    console.log(`Admin Dashboard 1366x768 diff ratio: ${(diff / (bMeta.width * bMeta.height) * 100).toFixed(3)}% (${diff} px)`);
  }

  // Compare products
  const baseProd = path.join(BASELINE_DIR, 'admin_products_1366x768.png');
  if (fs.existsSync(baseProd)) {
    const bImg = sharp(baseProd);
    const cImg = sharp(currentProd);
    const bMeta = await bImg.metadata();
    const bBuf = await bImg.raw().toBuffer();
    const cBuf = await cImg.raw().toBuffer();
    let diff = 0;
    for (let i = 0; i < bBuf.length; i += 4) {
      if (Math.abs(bBuf[i]-cBuf[i]) + Math.abs(bBuf[i+1]-cBuf[i+1]) + Math.abs(bBuf[i+2]-cBuf[i+2]) > 40) diff++;
    }
    console.log(`Admin Products 1366x768 diff ratio: ${(diff / (bMeta.width * bMeta.height) * 100).toFixed(3)}% (${diff} px)`);
  }
}

checkAdminDiff();
