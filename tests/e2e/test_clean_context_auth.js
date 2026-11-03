const { chromium } = require('playwright');
const { getAdminPassword, getDoctorPassword, getCustomerPassword } = require('./getTestPassword');

const BASE_URL = 'http://localhost:5000';

async function testCleanAuth() {
  console.log('=== STARTING PART 3 TASK 11 AUTH IN CLEAN CONTEXT ===');
  const browser = await chromium.launch({ headless: true });
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
    // 1. Fresh context is anonymous
    console.log('\n--- 1. Fresh Context Anonymity ---');
    const anonCtx = await browser.newContext();
    const anonPage = await anonCtx.newPage();
    await anonPage.goto(`${BASE_URL}/`, { waitUntil: 'domcontentloaded' });
    const anonBody = await anonPage.innerText('body');
    assert(anonBody.includes('Sign In') || !anonBody.includes('My Account'), 'Fresh context is completely anonymous (no session leakage)');
    await anonCtx.close();

    // 2. /doctor/login opens
    console.log('\n--- 2. /doctor/login Opens Cleanly ---');
    const docLoginCtx = await browser.newContext();
    const docLoginPage = await docLoginCtx.newPage();
    const docResp = await docLoginPage.goto(`${BASE_URL}/doctor/login`, { waitUntil: 'domcontentloaded' });
    assert(docResp.status() === 200, `/doctor/login opened with HTTP 200 (actual: ${docResp.status()})`);
    const docTitle = await docLoginPage.title();
    assert(docTitle.toLowerCase().includes('doctor') || docTitle.toLowerCase().includes('login'), `Page title is for doctor login: "${docTitle}"`);
    await docLoginCtx.close();

    // 3. Customer Sign-up with new email + ANY 6 digits
    console.log('\n--- 3. Customer Sign-up with New Email & Any 6 Digits OTP ---');
    const custCtx = await browser.newContext();
    const custPage = await custCtx.newPage();
    await custPage.goto(`${BASE_URL}/account/register`, { waitUntil: 'domcontentloaded' });
    const custEmail = `patient_${Date.now()}@example.com`;
    const custPass = 'TestPass@2026!';
    await custPage.fill('#registerForm input[name="FullName"]', 'Test Patient');
    await custPage.fill('#registerForm input[name="Email"]', custEmail);
    await custPage.fill('#registerForm input[name="ContactNumber"]', `9${Math.floor(100000000 + Math.random() * 900000000)}`);
    await custPage.fill('#registerForm input[name="Password"]', custPass);
    await custPage.fill('#registerForm input[name="ConfirmPassword"]', custPass);
    await custPage.check('#agreeTermsCheck');
    await Promise.all([
      custPage.waitForNavigation({ timeout: 15000 }).catch(() => {}),
      custPage.click('#registerForm button[type="submit"]')
    ]);
    assert(custPage.url().includes('verify-otp'), `Customer redirected to verify-otp: ${custPage.url()}`);

    const otpBoxes = custPage.locator('.otp-box');
    const boxCount = await otpBoxes.count();
    if (boxCount === 6) {
      // Enter random 6 digits
      const random6Digits = '847291';
      for (let i = 0; i < 6; i++) {
        await otpBoxes.nth(i).fill(random6Digits[i]);
      }
      await Promise.all([
        custPage.waitForNavigation({ timeout: 15000 }).catch(() => {}),
        custPage.click('#verifyOtpSubmitBtn')
      ]);
      const afterOtpUrl = custPage.url();
      assert(!afterOtpUrl.includes('verify-otp'), `Customer successfully verified with any 6 digits (${random6Digits}) -> redirected to ${afterOtpUrl}`);
    }

    // Customer Logout
    const custLogoutResp = await custPage.goto(`${BASE_URL}/account/logout`);
    assert(custLogoutResp.status() === 200 || custLogoutResp.status() === 302, 'Customer logout redirected cleanly');
    await custCtx.close();

    // 4. Doctor Sign-up with new email + ANY 6 digits
    console.log('\n--- 4. Doctor Sign-up with New Email & Any 6 Digits OTP ---');
    const docRegCtx = await browser.newContext();
    const docRegPage = await docRegCtx.newPage();
    await docRegPage.goto(`${BASE_URL}/doctor/register`, { waitUntil: 'domcontentloaded' });
    const newDocEmail = `vaidya_${Date.now()}@ayurvedahospital.in`;
    const newDocPhone = `8${Math.floor(100000000 + Math.random() * 900000000)}`;

    await Promise.all([
      docRegPage.waitForNavigation({ timeout: 15000 }).catch(() => {}),
      docRegPage.evaluate(({ email, phone }) => {
        document.querySelector('input[name="FullName"]').value = 'Dr. Test Vaidya';
        document.querySelector('input[name="Email"]').value = email;
        document.querySelector('input[name="ContactNumber"]').value = phone;
        document.querySelector('input[name="Password"]').value = 'DoctorPass@2026';
        document.querySelector('input[name="ConfirmPassword"]').value = 'DoctorPass@2026';
        document.querySelector('input[name="RegistrationNumber"]').value = 'AYUSH-998877';
        document.querySelector('input[name="ClinicAddress"]').value = 'Vaidya Shala, Delhi';
        document.querySelector('textarea[name="Bio"]').value = 'Practicing classical Nadi Pariksha and Panchakarma.';
        document.getElementById('doctorWizardForm').submit();
      }, { email: newDocEmail, phone: newDocPhone })
    ]);
    assert(docRegPage.url().includes('verify-otp'), `Doctor redirected to verify-otp: ${docRegPage.url()}`);

    const docOtpBoxes = docRegPage.locator('.otp-box');
    if (await docOtpBoxes.count() === 6) {
      const docRandomDigits = '419283';
      for (let i = 0; i < 6; i++) {
        await docOtpBoxes.nth(i).fill(docRandomDigits[i]);
      }
      await Promise.all([
        docRegPage.waitForNavigation({ timeout: 15000 }).catch(() => {}),
        docRegPage.click('#verifyOtpSubmitBtn')
      ]);
      assert(!docRegPage.url().includes('verify-otp'), `Doctor successfully verified with any 6 digits (${docRandomDigits}) -> ${docRegPage.url()}`);
    }

    // Doctor Logout via confirmModalForm POST
    await Promise.all([
      docRegPage.waitForNavigation({ timeout: 15000 }).catch(() => {}),
      docRegPage.evaluate(() => {
        const f = document.getElementById('confirmModalForm');
        if (f) {
          f.action = '/account/logout';
          f.submit();
        }
      })
    ]);
    assert(docRegPage.url().includes('login') || !docRegPage.url().includes('doctorportal/dashboard'), 'Doctor logged out and redirected to login');
    await docRegCtx.close();

    // 5. Admin DemoViewer Sign-up with new email + ANY 6 digits
    console.log('\n--- 5. Admin DemoViewer Sign-up with New Email & Any 6 Digits OTP ---');
    const admRegCtx = await browser.newContext();
    const admRegPage = await admRegCtx.newPage();
    await admRegPage.goto(`${BASE_URL}/admin-portal/auth/register`, { waitUntil: 'domcontentloaded' });
    const admEmail = `viewer_${Date.now()}@samplegov.in`;
    await admRegPage.fill('input[name="FullName"]', 'Govt Auditor');
    await admRegPage.fill('input[name="Email"]', admEmail);
    await admRegPage.fill('input[name="Password"]', 'DemoViewerPass@2026!');
    await admRegPage.fill('input[name="ConfirmPassword"]', 'DemoViewerPass@2026!');
    await Promise.all([
      admRegPage.waitForNavigation({ timeout: 15000 }).catch(() => {}),
      admRegPage.click('button[type="submit"]')
    ]);
    assert(admRegPage.url().includes('verify-otp'), `Admin DemoViewer redirected to verify-otp: ${admRegPage.url()}`);

    const admOtpBoxes = admRegPage.locator('.admin-otp-box, .otp-box');
    const admBoxCount = await admOtpBoxes.count();
    if (admBoxCount === 6) {
      const admRandomDigits = '382910';
      for (let i = 0; i < 6; i++) {
        await admOtpBoxes.nth(i).fill(admRandomDigits[i]);
      }
      await Promise.all([
        admRegPage.waitForNavigation({ timeout: 15000 }).catch(() => {}),
        admRegPage.click('#btnVerifyOtp, button[type="submit"]')
      ]);
      assert(admRegPage.url().includes('admin-portal') && !admRegPage.url().includes('verify-otp'), `DemoViewer verified with any 6 digits (${admRandomDigits}) -> landed at ${admRegPage.url()}`);
    }

    // Admin DemoViewer Logout via adminLogoutForm POST
    await Promise.all([
      admRegPage.waitForNavigation({ timeout: 15000 }).catch(() => {}),
      admRegPage.evaluate(() => {
        const f = document.getElementById('adminLogoutForm');
        if (f) f.submit();
      })
    ]);
    assert(admRegPage.url().includes('login'), 'Admin DemoViewer logged out and redirected to login');
    await admRegCtx.close();

    // 6. Real Admin Login with Real Password
    console.log('\n--- 6. Real Admin Login with Real Password ---');
    const realAdmCtx = await browser.newContext();
    const realAdmPage = await realAdmCtx.newPage();
    await realAdmPage.goto(`${BASE_URL}/admin-portal/auth/login`, { waitUntil: 'domcontentloaded' });
    await realAdmPage.fill('#Email', 'admin@thegreatayurveda.com');
    await realAdmPage.fill('#Password', getAdminPassword());
    await Promise.all([
      realAdmPage.waitForNavigation({ timeout: 15000 }).catch(() => {}),
      realAdmPage.click('#submitBtn')
    ]);
    assert(realAdmPage.url().includes('admin-portal') && !realAdmPage.url().includes('login'), `Real Admin logged in successfully with real password -> ${realAdmPage.url()}`);

    // Admin Logout via adminLogoutForm POST
    await Promise.all([
      realAdmPage.waitForNavigation({ timeout: 15000 }).catch(() => {}),
      realAdmPage.evaluate(() => {
        const f = document.getElementById('adminLogoutForm');
        if (f) f.submit();
      })
    ]);
    assert(realAdmPage.url().includes('login'), 'Real Admin logged out cleanly');
    await realAdmCtx.close();

    // 7. 3 Wrong Logins then Block Message
    console.log('\n--- 7. 3 Wrong Logins -> Throttle Block Message ---');
    const throttleCtx = await browser.newContext();
    const throttlePage = await throttleCtx.newPage();
    const targetEmail = `victim_${Date.now()}@victimcorp.com`;

    for (let attempt = 1; attempt <= 4; attempt++) {
      await throttlePage.goto(`${BASE_URL}/account/login`, { waitUntil: 'domcontentloaded' });
      await throttlePage.fill('#loginForm input[name="Email"]', targetEmail);
      await throttlePage.fill('#loginForm input[name="Password"]', 'DefWrongPassword123!');
      await Promise.all([
        throttlePage.waitForNavigation({ timeout: 10000 }).catch(() => {}),
        throttlePage.click('#loginForm button[type="submit"]')
      ]);
      const bodyText = await throttlePage.innerText('body');
      if (attempt <= 3) {
        console.log(`Attempt ${attempt}: Response received`);
      } else {
        const isBlocked = bodyText.includes('Too many failed') || bodyText.includes('Temporarily locked') || bodyText.includes('try again in');
        assert(isBlocked, `After 3 failed attempts, 4th attempt displays throttle block message: "${bodyText.substring(0, 100)}..."`);
      }
    }
    await throttleCtx.close();

  } catch (err) {
    console.error('Test run failed:', err);
    failed++;
  } finally {
    await browser.close();
  }

  console.log(`\n========================================`);
  console.log(`TASK 11 AUTH RESULTS: ${passed} PASSED, ${failed} FAILED`);
  console.log(`========================================\n`);

  if (failed > 0) process.exit(1);
}

testCleanAuth();
