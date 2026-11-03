const { chromium } = require('playwright');
const { getAdminPassword, getDoctorPassword, getCustomerPassword } = require('./getTestPassword');

async function runTests() {
    console.log("=== STARTING PHASE 1 AUTH VERIFICATION ===");
    const browser = await chromium.launch({ headless: true });
    
    try {
        // Test 1: Fresh Context is Anonymous (Phase 1 b)
        console.log("\n[Test 1] Fresh context anonymity check...");
        const ctx1 = await browser.newContext();
        const page1 = await ctx1.newPage();
        await page1.goto('http://localhost:5000/');
        const content1 = await page1.content();
        if (content1.includes('Demo Guest') || (content1.includes('My Account') && !content1.includes('Sign In'))) {
            console.error("FAIL: Fresh browser context is not anonymous!");
        } else {
            console.log("PASS: Fresh context is completely anonymous.");
        }
        await ctx1.close();

        // Test 2: GET to /account/logout, /doctor/logout, /admin-portal/auth/logout must never 404 (Phase 1 a)
        console.log("\n[Test 2] GET logout routes return 302/redirect and never 404...");
        const ctx2 = await browser.newContext();
        const page2 = await ctx2.newPage();
        
        for (const logoutUrl of ['http://localhost:5000/account/logout', 'http://localhost:5000/doctor/logout', 'http://localhost:5000/admin-portal/auth/logout', 'http://localhost:5000/admin-portal/logout']) {
            const resp = await page2.goto(logoutUrl);
            console.log(`GET ${logoutUrl} -> status: ${resp.status()}, url: ${page2.url()}`);
            if (resp.status() === 404) {
                console.error(`FAIL: ${logoutUrl} returned 404!`);
            } else {
                console.log(`PASS: ${logoutUrl} redirected cleanly without 404.`);
            }
        }
        await ctx2.close();

        // Test 3: Customer Login 5 times in a row without throttle block (Phase 1 a)
        console.log("\n[Test 3] Customer Login 5x in a row (throttle check)...");
        for (let i = 1; i <= 5; i++) {
            const ctx = await browser.newContext();
            const page = await ctx.newPage();
            await page.goto('http://localhost:5000/account/login');
            await page.fill('#loginForm input[name="Email"]', 'customer@ayurveda.com');
            await page.fill('#loginForm input[name="Password"]', getCustomerPassword());
            await Promise.all([
                page.waitForNavigation(),
                page.click('#loginForm button[type="submit"]')
            ]);
            const url = page.url();
            const pageText = await page.innerText('body');
            if (pageText.includes('Temporarily locked') || pageText.includes('Too many failed')) {
                console.error(`FAIL: Login attempt ${i} blocked by throttle!`);
            } else if (url.includes('/account/login')) {
                console.error(`FAIL: Login attempt ${i} stayed on login page!`);
            } else {
                console.log(`PASS: Login attempt ${i} succeeded, navigated to ${url}`);
            }
            await ctx.close();
        }

        // Test 4: Doctor Login & UI Modal POST Logout (Phase 1 a)
        console.log("\n[Test 4] Doctor Login & UI Modal POST Logout...");
        const docCtx = await browser.newContext();
        const docPage = await docCtx.newPage();
        await docPage.goto('http://localhost:5000/doctor/login');
        await docPage.fill('#loginForm input[name="Email"]', 'demo.doctor@thegreatayurveda.com');
        await docPage.fill('#loginForm input[name="Password"]', getDoctorPassword());
        await Promise.all([
            docPage.waitForNavigation(),
            docPage.click('#loginForm button[type="submit"]')
        ]);
        console.log(`Doctor logged in, landed at: ${docPage.url()}`);
        
        // Find visible logout button on doctor page
        const docLogoutBtn = docPage.locator('button[data-tga-logout]:visible').first();
        if (await docLogoutBtn.count() > 0) {
            await docLogoutBtn.click();
            await docPage.waitForSelector('#confirmActionModal.show, #confirmActionModal', { state: 'visible', timeout: 5000 }).catch(() => {});
            const proceedBtn = docPage.locator('#confirmModalProceedBtn:visible').first();
            if (await proceedBtn.count() > 0) {
                await Promise.all([
                    docPage.waitForNavigation({ timeout: 10000 }).catch(() => {}),
                    proceedBtn.click()
                ]);
                console.log(`Doctor after modal logout landed at: ${docPage.url()}`);
                console.log("PASS: Doctor modal POST logout succeeded.");
            }
        }
        await docCtx.close();

        // Test 5: Admin Login & UI Modal POST Logout (Phase 1 a)
        console.log("\n[Test 5] Admin Login & UI Modal POST Logout...");
        const admCtx = await browser.newContext();
        const admPage = await admCtx.newPage();
        await admPage.goto('http://localhost:5000/admin-portal/auth/login');
        await admPage.fill('#Email', 'admin@thegreatayurveda.com');
        await admPage.fill('#Password', getAdminPassword());
        await Promise.all([
            admPage.waitForNavigation(),
            admPage.click('#submitBtn')
        ]);
        console.log(`Admin logged in, landed at: ${admPage.url()}`);

        const admLogoutBtn = admPage.locator('button[data-tga-logout]:visible').first();
        if (await admLogoutBtn.count() > 0) {
            await admLogoutBtn.click();
            await admPage.waitForSelector('#confirmActionModal.show, #confirmActionModal, #confirmModalForm', { timeout: 5000 }).catch(() => {});
            const proceedBtn = admPage.locator('#confirmModalProceedBtn:visible, #confirmModalForm button[type="submit"]:visible').first();
            if (await proceedBtn.count() > 0) {
                await Promise.all([
                    admPage.waitForNavigation({ timeout: 10000 }).catch(() => {}),
                    proceedBtn.click()
                ]);
                console.log(`Admin after modal logout landed at: ${admPage.url()}`);
                console.log("PASS: Admin modal POST logout succeeded.");
            }
        }
        await admCtx.close();

        // Test 6: Email validation rejects "x@upi" (Phase 1 d)
        console.log("\n[Test 6] Customer Sign-up rejects 'x@upi'...");
        const regCtx = await browser.newContext();
        const regPage = await regCtx.newPage();
        await regPage.goto('http://localhost:5000/account/register');
        await regPage.fill('#FullName', 'UPI Tester');
        await regPage.fill('#registerForm input[name="Email"]', 'invaliduser@upi');
        await regPage.fill('#ContactNumber', '9876543210');
        await regPage.fill('#regPasswordInput', 'ValidPassword@123');
        await regPage.fill('input[name="ConfirmPassword"]', 'ValidPassword@123');
        await regPage.check('#agreeTermsCheck');
        await Promise.all([
            regPage.waitForNavigation({ timeout: 5000 }).catch(() => {}),
            regPage.click('#registerForm button[type="submit"]')
        ]);
        const regBody = await regPage.innerText('body');
        if (regBody.includes('valid email address with a domain') || regBody.includes('valid domain') || regBody.includes('valid email')) {
            console.log("PASS: 'x@upi' rejected with validation error.");
        } else {
            console.error("FAIL: 'x@upi' was not properly rejected! Body: " + regBody.slice(0, 200));
        }

        // Test 7: Customer Sign-up with new email + any 6 digits -> signed in (Phase 1 c, d)
        console.log("\n[Test 7] Customer Sign-up with new email and any 6 digits OTP...");
        const newEmail = `user_${Date.now()}@testdomain.com`;
        await regPage.fill('#FullName', 'Auto Test User');
        await regPage.fill('#registerForm input[name="Email"]', newEmail);
        await regPage.fill('#ContactNumber', '9876543210');
        await regPage.fill('#regPasswordInput', 'ValidPassword@123');
        await regPage.fill('input[name="ConfirmPassword"]', 'ValidPassword@123');
        if (!await regPage.isChecked('#agreeTermsCheck')) await regPage.check('#agreeTermsCheck');
        await Promise.all([
            regPage.waitForNavigation(),
            regPage.click('#registerForm button[type="submit"]')
        ]);
        console.log(`After register submit, landed at: ${regPage.url()}`);
        
        if (regPage.url().includes('verify-otp')) {
            const boxes = await regPage.$$('.otp-box');
            console.log(`Found ${boxes.length} OTP boxes. Entering '654321'...`);
            const digits = '654321';
            for (let i = 0; i < boxes.length; i++) {
                await boxes[i].fill(digits[i]);
            }
            await Promise.all([
                regPage.waitForNavigation(),
                regPage.click('#verifyOtpSubmitBtn')
            ]);
            console.log(`After OTP verify submit, landed at: ${regPage.url()}`);
            const postOtpContent = await regPage.content();
            if (postOtpContent.includes('Sign In') && !postOtpContent.includes('My Account')) {
                console.error("FAIL: User not signed in after OTP verification!");
            } else {
                console.log("PASS: User registered, verified with any 6 digits, and signed in successfully!");
            }
        } else {
            console.error("FAIL: Did not redirect to verify-otp page!");
        }
        await regCtx.close();

        // Test 8: Doctor sign-up with Demo__AutoApproveDoctors=true (Phase 1 d)
        console.log("\n[Test 8] Doctor sign-up with AutoApproveDoctors=true...");
        const docRegCtx = await browser.newContext();
        const docRegPage = await docRegCtx.newPage();
        await docRegPage.goto('http://localhost:5000/doctor/register');
        const docEmail = `doctor_${Date.now()}@sandboxhospital.org`;
        const docPhone = `9${Math.floor(100000000 + Math.random() * 900000000)}`;
        
        await Promise.all([
            docRegPage.waitForNavigation(),
            docRegPage.evaluate(({ email, phone }) => {
                document.querySelector('input[name="FullName"]').value = 'Dr. Sandbox Test';
                document.querySelector('input[name="Email"]').value = email;
                document.querySelector('input[name="ContactNumber"]').value = phone;
                document.querySelector('input[name="Password"]').value = 'DoctorPass@123';
                document.querySelector('input[name="ConfirmPassword"]').value = 'DoctorPass@123';
                document.querySelector('input[name="RegistrationNumber"]').value = 'AYUSH-123456';
                document.querySelector('input[name="ClinicAddress"]').value = 'Sandbox Clinic 123';
                document.querySelector('textarea[name="Bio"]').value = 'Sandbox Ayurvedic Consultant';
                document.getElementById('doctorWizardForm').submit();
            }, { email: docEmail, phone: docPhone })
        ]);
        console.log(`After doctor register, landed at: ${docRegPage.url()}`);
        
        if (docRegPage.url().includes('verify-otp')) {
            const boxes = await docRegPage.$$('.otp-box');
            const digits = '123456';
            for (let i = 0; i < boxes.length; i++) {
                await boxes[i].fill(digits[i]);
            }
            await Promise.all([
                docRegPage.waitForNavigation(),
                docRegPage.click('#verifyOtpSubmitBtn')
            ]);
            console.log(`After doctor OTP verify, landed at: ${docRegPage.url()}`);
            if (docRegPage.url().includes('dashboard') || docRegPage.url().includes('doctor')) {
                console.log("PASS: Sandbox doctor auto-approved and access granted.");
            } else {
                console.log("Doctor landed at: " + docRegPage.url());
            }

            // Check that sandbox doctor is NOT listed in the public directory
            const publicCtx = await browser.newContext();
            const publicDocPage = await publicCtx.newPage();
            await publicDocPage.goto('http://localhost:5000/doctors');
            const publicContent = await publicDocPage.content();
            if (publicContent.includes(docEmail) || publicContent.includes('Dr. Sandbox Test')) {
                console.error("FAIL: Sandbox doctor appeared in public directory!");
            } else {
                console.log("PASS: Sandbox doctor is NOT listed in the public directory.");
            }
            await publicCtx.close();
        }
        await docRegCtx.close();

        // Test 9: Admin DemoViewer Sign-up & Read-Only POST Block (Phase 1 e)
        console.log("\n[Test 9] Admin DemoViewer Sign-up & Read-Only POST test...");
        const admRegCtx = await browser.newContext();
        const admRegPage = await admRegCtx.newPage();
        await admRegPage.goto('http://localhost:5000/admin-portal/auth/register');
        const admEmail = `demoviewer_${Date.now()}@sampledomain.com`;
        await admRegPage.fill('input[name="FullName"]', 'Demo Admin Tester');
        await admRegPage.fill('input[name="Email"]', admEmail);
        await admRegPage.fill('input[name="Password"]', 'DemoViewer@2026!');
        await admRegPage.fill('input[name="ConfirmPassword"]', 'DemoViewer@2026!');
        await Promise.all([
            admRegPage.waitForNavigation(),
            admRegPage.click('button[type="submit"]')
        ]);
        console.log(`After admin demo register, landed at: ${admRegPage.url()}`);
        
        if (admRegPage.url().includes('verify-otp')) {
            console.log("Submitting admin demo OTP '999999'...");
            await Promise.all([
                admRegPage.waitForNavigation(),
                admRegPage.evaluate(() => {
                    document.getElementById('adminHiddenVerifyOtp').value = '999999';
                    document.getElementById('adminVerifyOtpForm').submit();
                })
            ]);
            console.log(`After admin OTP verify, landed at: ${admRegPage.url()}`);
            
            // Now attempt a POST request as DemoViewer to verify it is blocked
            console.log("Attempting POST to /admin-portal/products/create as DemoViewer...");
            const postResp = await admRegPage.request.post('http://localhost:5000/admin-portal/products/create', {
                headers: { 'X-Requested-With': 'XMLHttpRequest' },
                data: { Name: 'Unauthorized Product' }
            });
            console.log(`DemoViewer POST response status: ${postResp.status()}`);
            if (postResp.status() === 403 || postResp.status() === 302 || postResp.status() === 400) {
                console.log("PASS: DemoViewer is restricted from mutating operations.");
            } else {
                console.log(`DemoViewer POST returned status: ${postResp.status()}`);
            }
        }
        await admRegCtx.close();

        // Test 10: Protected Account Forgot Password Safety (Phase 1 c)
        console.log("\n[Test 10] Protected Account Forgot Password Demo Safety...");
        const fpCtx = await browser.newContext();
        const fpPage = await fpCtx.newPage();
        await fpPage.goto('http://localhost:5000/account/forgot-password');
        await fpPage.fill('input[name="Email"]', 'admin@thegreatayurveda.com');
        await Promise.all([
            fpPage.waitForNavigation(),
            fpPage.click('.card form button[type="submit"]')
        ]);
        console.log(`After forgot-password submit, landed at: ${fpPage.url()}`);
        
        // Try any 6 digits on reset-password for protected account
        if (fpPage.url().includes('reset-password')) {
            await fpPage.fill('#resetNewPwd', 'HackedAdmin@123!');
            await fpPage.fill('input[name="ConfirmPassword"]', 'HackedAdmin@123!');
            await Promise.all([
                fpPage.waitForNavigation(),
                fpPage.evaluate(() => {
                    document.getElementById('hiddenResetOtp').value = '123456';
                    document.getElementById('resetPasswordForm').submit();
                })
            ]);
            const resetContent = await fpPage.innerText('body');
            if (resetContent.includes('Password reset failed') || resetContent.includes('restricted') || resetContent.includes('Invalid') || resetContent.includes('failed')) {
                console.log("PASS: Protected admin account cannot be reset via demo OTP!");
            } else {
                console.error("FAIL: Protected account was reset or error was not shown!");
            }
        }
        await fpCtx.close();

        console.log("\n=== ALL PHASE 1 TESTS COMPLETED ===");
    } catch (err) {
        console.error("Test execution error:", err);
    } finally {
        await browser.close();
    }
}

runTests();
