const { chromium } = require('playwright');

const BASE_URL = 'http://localhost:5000';

const pagesToAudit = [
  { url: '/', name: 'Home (/)' },
  { url: '/shop', name: 'Shop Catalog (/shop)' },
  { url: '/shop/details/308', name: 'Product Details (/shop/details/308)' },
  { url: '/cart', name: 'Cart (/cart)' },
  { url: '/doctors', name: 'Doctors Directory (/doctors)' },
  { url: '/doctors/profile/1', name: 'Doctor Profile (/doctors/profile/1)' },
  { url: '/yoga', name: 'Yoga Index (/yoga)' },
  { url: '/yoga/details/1', name: 'Yoga Details (/yoga/details/1)' },
  { url: '/search?q=ashwagandha', name: 'Search Results (/search)' },
  { url: '/about-project', name: 'About Project (/about-project)' },
  { url: '/credits', name: 'Credits (/credits)' },
  { url: '/account/login', name: 'Customer Login (/account/login)' },
  { url: '/account/register', name: 'Customer Register (/account/register)' },
  { url: '/account/verify-otp', name: 'Verify OTP (/account/verify-otp)' },
  { url: '/account/forgot-password', name: 'Forgot Password (/account/forgot-password)' },
  { url: '/doctor/login', name: 'Doctor Portal Login (/doctor/login)' },
  { url: '/account/doctor-register', name: 'Doctor Register (/account/doctor-register)' },
  { url: '/doctors/book/1', name: 'Book Doctor (/doctors/book/1)' },
  { url: '/doctors/room/1', name: 'Tele-Consultation Call Room (/doctors/room/1)' },
  { url: '/checkout', name: 'Checkout (/checkout)' },
  { url: '/checkout/confirmation?orderId=1', name: 'Order Confirmation (/checkout/confirmation)' },
  { url: '/orders/details/1', name: 'Order Tracking (/orders/details/1)' },
  { url: '/doctor-portal/dashboard', name: 'Doctor Portal Dashboard (/doctor-portal/dashboard)' },
  { url: '/admin-portal/auth/login', name: 'Admin Portal Login (/admin-portal/auth/login)' },
  { url: '/admin-portal/dashboard', name: 'Admin Portal Dashboard (/admin-portal/dashboard)' }
];

async function generateTable() {
  const browser = await chromium.launch({ headless: true });
  const context = await browser.newContext({ viewport: { width: 1280, height: 800 } });
  const page = await context.newPage();

  console.log('| # | Page Name & Route | Ad Present? | Expected Ad Status | Compliance |');
  console.log('|---|---|---|---|---|');

  let idx = 1;
  for (const p of pagesToAudit) {
    try {
      await page.goto(`${BASE_URL}${p.url}`, { waitUntil: 'domcontentloaded', timeout: 15000 });
      await page.waitForTimeout(300);
      const adCount = await page.locator('.tga-demo-ad-wrapper, #tgaStickyBottomAd').count();
      const hasAd = adCount > 0;

      const isSuppressedPage = p.url.includes('/checkout') ||
                               p.url.includes('/account') ||
                               p.url.includes('/doctor/login') ||
                               p.url.includes('/doctors/book') ||
                               p.url.includes('/doctors/room') ||
                               p.url.includes('/doctor-portal') ||
                               p.url.includes('/admin-portal');

      const expected = isSuppressedPage ? 'Suppressed (No Ad)' : 'Allowed (Ad Shown)';
      const statusMatch = (isSuppressedPage && !hasAd) || (!isSuppressedPage && hasAd);
      const compliance = statusMatch ? 'PASS' : 'INSPECT';

      console.log(`| ${idx++} | ${p.name} | ${hasAd ? 'YES (' + adCount + ')' : 'NO (0)'} | ${expected} | ${compliance} |`);
    } catch (e) {
      console.log(`| ${idx++} | ${p.name} | ERROR (${e.message.split('\n')[0]}) | Unknown | ERROR |`);
    }
  }

  await browser.close();
}

generateTable().catch(console.error);
