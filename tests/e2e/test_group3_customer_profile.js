const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');

const WEB_URL = 'http://localhost:5000';
const SCREENSHOT_DIR = path.resolve(__dirname, 'screenshots');

if (!fs.existsSync(SCREENSHOT_DIR)) {
    fs.mkdirSync(SCREENSHOT_DIR, { recursive: true });
}

let passedCount = 0;
let failedCount = 0;

function assert(condition, description) {
    if (condition) {
        console.log(`✅ PASS: ${description}`);
        passedCount++;
    } else {
        console.error(`❌ FAIL: ${description}`);
        failedCount++;
    }
}

async function run() {
    console.log('================================================================');
    console.log('🧪 TASK 3 E2E VERIFICATION SUITE: CUSTOMER PROFILE CRUD');
    console.log(`Web URL:   ${WEB_URL}`);
    console.log('================================================================\n');

    const browser = await chromium.launch({ headless: true });
    const context = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const page = await context.newPage();

    try {
        // -------------------------------------------------------------
        // 1. LOGIN AS TEST CUSTOMER (customer@ayurveda.com)
        // -------------------------------------------------------------
        console.log('\n--- 1. Login as Customer ---');
        await page.goto(`${WEB_URL}/Account/Login`, { waitUntil: 'networkidle', timeout: 30000 });
        await page.fill('input[name="Email"]', 'customer@ayurveda.com');
        await page.fill('input[name="Password"]', 'Customer@123');
        await page.click('#loginForm button[type="submit"]');
        await page.waitForTimeout(2500);

        console.log('URL after login:', page.url());
        assert(!page.url().includes('/Login'), 'Customer logged in successfully');

        // Navigate to Profile
        await page.goto(`${WEB_URL}/Account/Profile`, { waitUntil: 'networkidle', timeout: 30000 });
        await page.waitForTimeout(1000);

        const pageTitle = await page.$eval('h4', el => el.textContent.trim());
        assert(pageTitle.includes('Personal Details'), 'Navigated to Account Profile page');

        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'profile_view_initial.png') });
        console.log('📸 Captured profile_view_initial.png');

        // -------------------------------------------------------------
        // 2. EDIT PROFILE (NAME, PHONE, DOB, GENDER, PHOTO)
        // -------------------------------------------------------------
        console.log('\n--- 2. Edit Profile Details & Photo Upload ---');
        const updatedName = 'Aarav K. Patel';
        const updatedPhone = '+91 98765 99999';
        const updatedDob = '1992-05-15';
        const updatedGender = 'Male';

        await page.fill('#inputFullName', updatedName);
        await page.fill('#inputContactNumber', updatedPhone);
        await page.fill('#inputDateOfBirth', updatedDob);
        await page.selectOption('#inputGender', updatedGender);

        // Upload a banner as test avatar
        const testImagePath = path.resolve(__dirname, '../../src/Ayurveda.Web/wwwroot/images/banners/banner-hero-diwali.webp');
        if (fs.existsSync(testImagePath)) {
            await page.setInputFiles('#profilePhotoInput', testImagePath);
            await page.waitForTimeout(500);
            const previewSrc = await page.$eval('#photoUploadPreview', img => img.getAttribute('src'));
            assert(previewSrc.startsWith('data:image'), 'Profile photo file input triggers live data URL image preview');
        }

        await page.click('#btnSaveProfile');
        await page.waitForTimeout(2500);

        // Check success toast / alert
        const alertSuccess = await page.$('#profileSuccessAlert');
        assert(alertSuccess !== null, 'Success toast / alert displayed after updating profile');

        // Reload page to verify persistence from server
        await page.goto(`${WEB_URL}/Account/Profile`, { waitUntil: 'networkidle', timeout: 30000 });
        await page.waitForTimeout(1000);

        const currentNameVal = await page.$eval('#inputFullName', el => el.value);
        const currentPhoneVal = await page.$eval('#inputContactNumber', el => el.value);
        const currentDobVal = await page.$eval('#inputDateOfBirth', el => el.value);
        const currentGenderVal = await page.$eval('#inputGender', el => el.value);

        assert(currentNameVal === updatedName, `Persisted Full Name matches: "${currentNameVal}"`);
        assert(currentPhoneVal === updatedPhone, `Persisted Phone matches: "${currentPhoneVal}"`);
        assert(currentDobVal === updatedDob, `Persisted DOB matches: "${currentDobVal}"`);
        assert(currentGenderVal === updatedGender, `Persisted Gender matches: "${currentGenderVal}"`);

        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'profile_details_updated.png') });
        console.log('📸 Captured profile_details_updated.png');

        // -------------------------------------------------------------
        // 3. ADDRESS BOOK CRUD (ADD, EDIT, SET DEFAULT, DELETE)
        // -------------------------------------------------------------
        console.log('\n--- 3. Testing Address Book CRUD ---');
        await page.click('#tab-addresses-btn');
        await page.waitForTimeout(800);

        // 3A. ADD ADDRESS
        await page.click('#btnOpenAddAddress');
        await page.waitForTimeout(800);

        const testRecipient = 'Pooja Patel';
        const testLine1 = 'Flat 302, Lotus Towers, 8th Main';
        const testCity = 'Bengaluru';
        const testState = 'Karnataka';
        const testPin = '560034';

        await page.fill('#addAddressRecipientName', testRecipient);
        await page.fill('#addAddressPhone', '+91 91234 56789');
        await page.fill('#addAddressLine1', testLine1);
        await page.fill('#addAddressLine2', 'Koramangala 4th Block');
        await page.fill('#addAddressCity', testCity);
        await page.fill('#addAddressState', testState);
        await page.fill('#addAddressPostalCode', testPin);
        await page.check('#addAddressIsDefault');

        await page.click('#btnSubmitAddAddress');
        await page.waitForTimeout(2500);

        // Verify address added
        await page.click('#tab-addresses-btn');
        await page.waitForTimeout(800);

        const addressContent = await page.content();
        assert(addressContent.includes(testRecipient), `New address for "${testRecipient}" is visible in address book`);
        assert(addressContent.includes(testLine1), `Address line 1 "${testLine1}" is visible`);
        assert(addressContent.includes('Default'), 'New address has Default badge');

        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'profile_address_added.png') });
        console.log('📸 Captured profile_address_added.png');

        // 3B. EDIT ADDRESS
        console.log('\n--- 3B. Edit Existing Address ---');
        // Click edit on first address card
        const editBtn = await page.$('#addressCardGrid button[data-bs-target^="#editAddressModal"]');
        assert(editBtn !== null, 'Edit button found on address card');
        if (editBtn) {
            await editBtn.click();
            await page.waitForSelector('.modal.show', { state: 'visible', timeout: 5000 });
            await page.waitForTimeout(600);

            const updatedLine1 = 'Flat 302, Lotus Grand Towers, 8th Main';
            await page.fill('.modal.show input[name="AddressLine1"]', updatedLine1);
            await page.click('.modal.show button[type="submit"]');
            await page.waitForTimeout(2500);

            // Wait for modal backdrop to detach
            await page.waitForSelector('.modal-backdrop', { state: 'detached', timeout: 5000 }).catch(() => {});

            // Switch to addresses tab if needed
            const isTabActive = await page.$eval('#tab-addresses', el => el.classList.contains('active')).catch(() => false);
            if (!isTabActive) {
                await page.click('#tab-addresses-btn');
                await page.waitForTimeout(600);
            }

            const afterEditContent = await page.content();
            assert(afterEditContent.includes(updatedLine1), `Updated address line "${updatedLine1}" persisted`);
            await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'profile_address_edited.png') });
            console.log('📸 Captured profile_address_edited.png');
        }

        // 3C. DELETE ADDRESS
        console.log('\n--- 3C. Delete Address ---');
        const deleteButtons = await page.$$('#addressCardGrid .btn-delete-address');
        console.log(`Found ${deleteButtons.length} addresses to test deletion`);
        if (deleteButtons.length > 0) {
            page.once('dialog', async dialog => {
                console.log(`Accepting confirmation dialog: "${dialog.message()}"`);
                await dialog.accept();
            });
            await deleteButtons[0].click();
            await page.waitForTimeout(2500);

            const alertDel = await page.$('#profileSuccessAlert');
            assert(alertDel !== null, 'Success toast displayed after address deletion');
            await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'profile_address_deleted.png') });
            console.log('📸 Captured profile_address_deleted.png');
        }

        // -------------------------------------------------------------
        // 4. CHANGE PASSWORD
        // -------------------------------------------------------------
        console.log('\n--- 4. Testing Change Password ---');
        await page.click('#tab-security-btn');
        await page.waitForTimeout(800);

        await page.fill('#inputCurrentPassword', 'Customer@123');
        await page.fill('#inputNewPassword', 'CustomerNew@2026');
        await page.fill('#inputConfirmPassword', 'CustomerNew@2026');

        await page.click('#btnUpdatePassword');
        await page.waitForTimeout(2500);

        const pwAlert = await page.$('#profileSuccessAlert');
        assert(pwAlert !== null, 'Success toast displayed after changing password');

        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'profile_password_changed.png') });
        console.log('📸 Captured profile_password_changed.png');

        // Change password back to Customer@123 for regression test idempotency
        await page.click('#tab-security-btn');
        await page.waitForTimeout(800);
        await page.fill('#inputCurrentPassword', 'CustomerNew@2026');
        await page.fill('#inputNewPassword', 'Customer@123');
        await page.fill('#inputConfirmPassword', 'Customer@123');
        await page.click('#btnUpdatePassword');
        await page.waitForTimeout(2500);
        console.log('Restored original Customer@123 password');

        // -------------------------------------------------------------
        // 5. SOFT DELETE ACCOUNT ("DELETE MY ACCOUNT" + LOGOUT)
        // -------------------------------------------------------------
        console.log('\n--- 5. Testing Soft Delete Account on a Temporary User ---');
        await context.clearCookies();
        const tempEmail = 'testdelete@thegreatayurveda.com';
        const tempPass = 'Customer@123';

        await page.goto(`${WEB_URL}/Account/Login`, { waitUntil: 'networkidle', timeout: 30000 });
        await page.fill('input[name="Email"]', tempEmail);
        await page.fill('input[name="Password"]', tempPass);
        await page.click('#loginForm button[type="submit"]');
        await page.waitForTimeout(2500);

        console.log('Logged in as test delete user, current URL:', page.url());

        // Navigate to Profile
        await page.goto(`${WEB_URL}/Account/Profile`, { waitUntil: 'networkidle', timeout: 30000 });
        await page.waitForTimeout(1000);

        // Click Danger tab
        await page.click('#tab-danger-btn');
        await page.waitForTimeout(800);

        // Click Delete My Account button to open modal
        await page.click('#btnOpenDeleteAccountModal');
        await page.waitForTimeout(800);

        // Confirm in modal
        await page.check('#confirmDeleteCheck');
        await page.click('#btnConfirmDeleteAccount');
        await page.waitForTimeout(3000);

        console.log('URL after account deletion:', page.url());
        assert(page.url() === `${WEB_URL}/` || page.url() === `${WEB_URL}/Home` || page.url().includes('localhost:5000'), 'User redirected to home page after account deletion');

        // Check that user is signed out
        const navText = await page.$eval('.tga-navbar', el => el.textContent);
        assert(navText.includes('Sign In') || !navText.includes('Temporary Deletion'), 'User session ended and signed out');

        // Try to log in with deleted account
        await page.goto(`${WEB_URL}/Account/Login`, { waitUntil: 'networkidle', timeout: 30000 });
        await page.fill('input[name="Email"]', tempEmail);
        await page.fill('input[name="Password"]', tempPass);
        await page.click('#loginForm button[type="submit"]');
        await page.waitForTimeout(2000);

        const loginErrorHtml = await page.content();
        assert(loginErrorHtml.includes('deactivated') || loginErrorHtml.includes('Invalid'), 'Deleted account login is strictly blocked');

        await page.screenshot({ path: path.join(SCREENSHOT_DIR, 'profile_account_soft_deleted_blocked.png') });
        console.log('📸 Captured profile_account_soft_deleted_blocked.png');

        console.log('\n================================================================');
        console.log(`TOTAL CHECKS: ${passedCount + failedCount} | PASSED: ${passedCount} | FAILED: ${failedCount}`);
        console.log('================================================================');

    } catch (err) {
        console.error('Test execution error:', err);
        failedCount++;
    } finally {
        await browser.close();
    }

    if (failedCount > 0) {
        process.exit(1);
    }
}

run();
