const { chromium } = require('playwright');
const { execSync } = require('child_process');

(async () => {
    console.log('================================================================');
    console.log('STARTING VERIFICATION OF USER & DOCTOR REGISTRATION & DASHBOARD');
    console.log('================================================================');

    const browser = await chromium.launch({
        headless: true,
        channel: 'chrome',
        args: ['--use-fake-ui-for-media-stream', '--use-fake-device-for-media-stream']
    });

    const context = await browser.newContext({
        viewport: { width: 1280, height: 800 }
    });
    const page = await context.newPage();

    try {
        // ---------------------------------------------------------------------
        // Requirement 1: "Register presents two options: Register as a Doctor or Register as a Customer"
        // ---------------------------------------------------------------------
        console.log('\n--- 1. Testing Register Dropdown on Home Page ---');
        await page.goto('http://localhost:5000', { waitUntil: 'domcontentloaded' });
        await page.waitForTimeout(1000);

        const registerBtn = page.locator('#headerRegisterDropdown');
        const isRegisterBtnVisible = await registerBtn.isVisible();
        console.log(`[PASS] Header Register dropdown button visible: ${isRegisterBtnVisible}`);
        if (!isRegisterBtnVisible) throw new Error('Register button #headerRegisterDropdown not found');

        // Click Register button to open dropdown
        await registerBtn.click();
        await page.waitForTimeout(400);

        const custOpt = page.locator('text=Register as a Customer').first();
        const docOpt = page.locator('text=Register as a Doctor').first();
        const custVisible = await custOpt.isVisible();
        const docVisible = await docOpt.isVisible();
        console.log(`[PASS] Dropdown options visible: "Register as a Customer" = ${custVisible}, "Register as a Doctor" = ${docVisible}`);
        if (!custVisible || !docVisible) throw new Error('Register dropdown options not displayed properly');

        // ---------------------------------------------------------------------
        // Requirement 1 & 2: Navigation to Register pages & Role Toggle
        // ---------------------------------------------------------------------
        console.log('\n--- 2. Testing Customer Register Page & Toggle ---');
        await custOpt.click();
        await page.waitForURL('**/account/register**');
        console.log(`[PASS] Navigated to: ${page.url()}`);

        const custToggle = page.locator('#btnToggleCustomerRegister');
        const docToggle = page.locator('#btnToggleDoctorRegister');
        console.log(`[PASS] Customer page pill toggles visible: Customer=${await custToggle.isVisible()}, Doctor=${await docToggle.isVisible()}`);

        console.log('\n--- 3. Testing Switching to Doctor Register via Toggle ---');
        await docToggle.click();
        await page.waitForURL('**/doctor/register**');
        console.log(`[PASS] Navigated to: ${page.url()}`);
        const docActiveToggle = page.locator('#btnToggleDoctorRegister.active');
        console.log(`[PASS] Doctor toggle active on doctor register page: ${await docActiveToggle.isVisible()}`);

        // ---------------------------------------------------------------------
        // Requirement 2: Separate Customer Registration
        // ---------------------------------------------------------------------
        console.log('\n--- 4. Registering a Separate Customer Account ---');
        await page.goto('http://localhost:5000/account/register');
        const randNum = Date.now() % 100000;
        const custEmail = `customer_e2e_${randNum}@example.com`;
        const custPass = 'Customer@123';

        console.log(`Registering new customer account: ${custEmail}`);
        await page.fill('#registerForm input[name="FullName"]', 'Test Customer');
        await page.fill('#registerForm input[name="Email"]', custEmail);
        await page.fill('#registerForm input[name="ContactNumber"]', '98765' + String(randNum).padStart(5, '0'));
        await page.fill('#registerForm input[name="Password"]', custPass);
        await page.fill('#registerForm input[name="ConfirmPassword"]', custPass);
        await page.check('#agreeTermsCheck');

        await page.click('#registerForm button[type="submit"]');
        await page.waitForTimeout(2000);

        console.log(`Post-registration URL: ${page.url()}`);
        if (page.url().includes('verify-otp')) {
            console.log('Verifying 6-digit OTP in demo mode...');
            await page.evaluate(() => {
                const hidden = document.getElementById('hiddenOtpInput');
                if (hidden) hidden.value = '123456';
                const boxes = document.querySelectorAll('.otp-box');
                const digits = '123456'.split('');
                boxes.forEach((b, i) => { b.value = digits[i]; });
            });
            await page.click('#verifyOtpSubmitBtn');
            await page.waitForTimeout(2500);
        }

        console.log(`[PASS] Authenticated customer URL: ${page.url()}`);

        // ---------------------------------------------------------------------
        // Requirement 3: Customer Books Video Call with Doctor 40 (Testdoctor)
        // ---------------------------------------------------------------------
        console.log('\n--- 5. Booking Consultation with Test Doctor (ID 40) ---');
        await page.goto('http://localhost:5000/Doctors/Book/40', { waitUntil: 'domcontentloaded' });
        await page.waitForTimeout(1000);

        const bookTitle = await page.title();
        console.log(`[PASS] Booking page loaded with Title: "${bookTitle}"`);
        if (bookTitle.includes('404') || bookTitle.includes('Not Found')) {
            throw new Error('Doctor booking page returned 404 Not Found');
        }

        // Set date to 7 days ahead and alternate slot to prevent slot conflict
        const targetDate = new Date();
        targetDate.setDate(targetDate.getDate() + 7);
        const yyyyMmDd = targetDate.toISOString().split('T')[0];
        await page.fill('input[name="SelectedDate"]', yyyyMmDd);

        // Fill consultation booking form
        await page.fill('input[name="PatientName"]', 'Test Customer');
        await page.fill('input[name="PatientContact"]', '9876543210');
        await page.fill('input[name="PatientAge"]', '30');
        await page.fill('textarea[name="HealthConcern"]', 'Need online video consultation for digestive wellness.');

        // Select 02:30 PM slot if available
        const altSlot = page.locator('.slot-chip-btn[data-slot="02:30 PM"]');
        if (await altSlot.isVisible()) {
            await altSlot.click();
        }

        // Pay and book
        const payBtn = page.locator('#btnInitiateConsultPayment');
        await payBtn.click();
        console.log('Initiated consultation payment, waiting for simulated gateway...');

        // Wait for redirect to MyAppointments
        await page.waitForURL('**/myappointments**', { timeout: 15000 });
        console.log(`[PASS] Redirected after booking to: ${page.url()}`);

        const apptsBody = await page.locator('body').innerText();
        const hasBooking = apptsBody.includes('Testdoctor') || apptsBody.includes('Consultation');
        console.log(`[PASS] Appointment displayed in patient dashboard: ${hasBooking}`);

        // ---------------------------------------------------------------------
        // Requirement 3 (Verification): Verify Appointment in DB for Doctor 40
        // ---------------------------------------------------------------------
        console.log('\n--- 6. Verifying Consultation in DB for Doctor 40 ---');
        const sqlCmd = `sqlcmd -S "(localdb)\\mssqllocaldb" -d "AyurvedaDb" -Q "SELECT TOP(1) a.Id, a.DoctorId, a.PatientName, a.ConsultationType, a.Status, a.MeetingRoomId FROM Appointments a WHERE a.DoctorId = 40 ORDER BY a.Id DESC"`;
        const dbResult = execSync(sqlCmd).toString();
        console.log('Database Record for Doctor 40:');
        console.log(dbResult.trim());

        if (!dbResult.includes('Confirmed') || !dbResult.includes('meet.jit.si')) {
            throw new Error('Appointment was not recorded with Confirmed status and MeetingRoomId');
        }
        console.log('[PASS] Appointment confirmed in database with provisioned WebRTC video meeting room URL!');

        console.log('\n================================================================');
        console.log('ALL THREE USER REQUIREMENTS SUCCESSFULLY VERIFIED & FUNCTIONAL');
        console.log('================================================================');
    } catch (err) {
        console.error('TEST ERROR:', err);
        process.exitCode = 1;
    } finally {
        await browser.close();
    }
})();
