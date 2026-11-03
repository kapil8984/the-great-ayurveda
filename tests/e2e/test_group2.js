const { chromium } = require('playwright');
const path = require('path');
const fs = require('fs');
const { getDevAdminPassword, getCustomerPassword } = require('./getTestPassword');

const WEB_URL = 'http://localhost:5000';
const ADMIN_URL = 'http://localhost:7200';
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
    console.log('🧪 GROUP 2 E2E VERIFICATION SUITE: DOCTOR AUTH & ROLE WORKFLOWS');
    console.log(`Web URL:   ${WEB_URL}`);
    console.log(`Admin URL: ${ADMIN_URL}`);
    console.log('================================================================\n');

    const browser = await chromium.launch({ headless: true });
    
    // Web Context (Patient & Doctor)
    const webContext = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const webPage = await webContext.newPage();

    // Admin Context
    const adminContext = await browser.newContext({ viewport: { width: 1440, height: 900 } });
    const adminPage = await adminContext.newPage();

    try {
        // -------------------------------------------------------------
        // 1. SEEDED DOCTOR LOGIN (dr.aarav.sharma@thegreatayurveda.com)
        // -------------------------------------------------------------
        console.log('\n--- 1. Testing Seeded Doctor Login ---');
        await webPage.goto(`${WEB_URL}/Account/Login?role=Doctor`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await webPage.fill('#loginForm input[name="Email"]', 'dr.aarav.sharma@thegreatayurveda.com');
        await webPage.fill('#loginForm input[name="Password"]', 'DevDemoPass_2026!#');
        
        await Promise.all([
            webPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }).catch(e => console.log('Doc login nav note:', e.message)),
            webPage.click('#loginForm button[type="submit"]')
        ]);
        await webPage.waitForTimeout(1500);

        console.log('URL after doctor login:', webPage.url());
        assert(webPage.url().includes('/DoctorPortal/Dashboard') || webPage.url().includes('/doctorportal/dashboard'), 'Seeded doctor logs in with DevDemoPass_2026!# and reaches Clinical Dashboard');
        
        const docDashboardHtml = await webPage.content();
        assert(docDashboardHtml.includes('Clinical Overview') || docDashboardHtml.includes('Dr.') || docDashboardHtml.includes('Appointment'), 'Doctor dashboard renders clinical desk details');

        await webPage.screenshot({ path: path.join(SCREENSHOT_DIR, 'group2_seeded_doctor_dashboard.png') });
        console.log('📸 Captured group2_seeded_doctor_dashboard.png');

        // -------------------------------------------------------------
        // 2. ROLE SEPARATION: DOCTOR CANNOT ACCESS CUSTOMER PAGES
        // -------------------------------------------------------------
        console.log('\n--- 2. Testing Role Separation: Doctor Blocked from Customer Checkout & Orders ---');
        await webPage.goto(`${WEB_URL}/Checkout`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await webPage.waitForTimeout(1000);
        console.log('Doctor navigated to /Checkout, current URL:', webPage.url());
        assert(!webPage.url().endsWith('/Checkout'), 'Doctor is blocked from retail customer Checkout page');

        await webPage.goto(`${WEB_URL}/Orders`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await webPage.waitForTimeout(1000);
        console.log('Doctor navigated to /Orders, current URL:', webPage.url());
        assert(!webPage.url().endsWith('/Orders'), 'Doctor is blocked from retail customer Orders page');

        await webPage.screenshot({ path: path.join(SCREENSHOT_DIR, 'group2_doctor_blocked_from_customer_checkout.png') });
        console.log('📸 Captured group2_doctor_blocked_from_customer_checkout.png');

        // Log doctor out
        await webContext.clearCookies();

        // -------------------------------------------------------------
        // 3. ROLE SEPARATION: CUSTOMER CANNOT ACCESS DOCTOR PORTAL
        // -------------------------------------------------------------
        console.log('\n--- 3. Testing Role Separation: Customer Blocked from Doctor Portal ---');
        await webPage.goto(`${WEB_URL}/Account/Login`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await webPage.fill('#loginForm input[name="Email"]', 'customer@ayurveda.com');
        await webPage.fill('#loginForm input[name="Password"]', getCustomerPassword());
        await Promise.all([
            webPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }).catch(e => {}),
            webPage.click('#loginForm button[type="submit"]')
        ]);
        await webPage.waitForTimeout(1500);

        // Try to access doctor dashboard
        await webPage.goto(`${WEB_URL}/DoctorPortal/Dashboard`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await webPage.waitForTimeout(1000);
        console.log('Customer navigated to /DoctorPortal/Dashboard, current URL:', webPage.url());
        assert(webPage.url().includes('AccessDenied') || webPage.url().includes('Login'), 'Customer cannot access Doctor Portal (redirected to AccessDenied/Login)');

        await webPage.screenshot({ path: path.join(SCREENSHOT_DIR, 'group2_customer_blocked_from_doctor_portal.png') });
        console.log('📸 Captured group2_customer_blocked_from_doctor_portal.png');

        // Clear web context cookies for registration test
        await webContext.clearCookies();

        // -------------------------------------------------------------
        // 4. NEW DOCTOR REGISTRATION (NO DEGREE UPLOAD - OPTIONAL)
        // -------------------------------------------------------------
        console.log('\n--- 4. Testing New Doctor Registration (Optional Degree Upload) ---');
        const timestamp = Date.now();
        const testDoctorEmail = `dr.newapp.${timestamp}@thegreatayurveda.com`;
        const testDoctorName = `Dr. Devrat Pathak ${timestamp.toString().slice(-4)}`;
        const testDoctorPhone = '987' + Math.floor(1000000 + Math.random() * 9000000);

        await webPage.goto(`${WEB_URL}/doctor/register`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await webPage.waitForTimeout(1000);

        const regHtml = await webPage.content();
        assert(regHtml.includes('Optional') || regHtml.includes('optional'), 'Degree certificate is marked Optional on doctor registration form');

        // Step 1: Account & Contact
        await webPage.fill('#wizardStep1 input[name="FullName"]', testDoctorName);
        await webPage.fill('#wizardStep1 input[name="Email"]', testDoctorEmail);
        await webPage.fill('#wizardStep1 input[name="ContactNumber"]', testDoctorPhone);
        await webPage.fill('#wizardStep1 input[name="Password"]', 'DevDemoPass_2026!#');
        await webPage.fill('#wizardStep1 input[name="ConfirmPassword"]', 'DevDemoPass_2026!#');
        await webPage.click('#wizardStep1 button:has-text("Next: Professional Details")');
        await webPage.waitForTimeout(500);

        // Step 2: Professional Details
        await webPage.selectOption('#wizardStep2 select[name="Qualification"]', 'MD (Ayurveda)');
        await webPage.fill('#wizardStep2 input[name="RegistrationNumber"]', `AYU-DL-2026-${timestamp.toString().slice(-4)}`);
        await webPage.fill('#wizardStep2 input[name="RegistrationCouncil"]', 'Delhi Bharatiya Chikitsa Parishad');
        await webPage.fill('#wizardStep2 input[name="Languages"]', 'Hindi, English');
        await webPage.fill('#wizardStep2 input[name="ClinicAddress"]', 'Panchakarma Bhavan, Connaught Place, New Delhi');
        await webPage.fill('#wizardStep2 textarea[name="Bio"]', 'Specialist in classical Virechana and Basti therapies.');
        await webPage.click('#wizardStep2 button:has-text("Next: Document Verification")');
        await webPage.waitForTimeout(500);

        // Step 3: Document Verification (Degree is completely OPTIONAL - skipped!)
        await webPage.click('#wizardStep3 button:has-text("Next: Schedule & Slots")');
        await webPage.waitForTimeout(500);

        // Step 4: Schedule & Slots
        await webPage.click('#wizardStep4 button:has-text("Next: Review & Submit")');
        await webPage.waitForTimeout(500);

        // Step 5: Final Submission
        await Promise.all([
            webPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }).catch(e => {}),
            webPage.click('#wizardStep5 button[type="submit"]')
        ]);
        await webPage.waitForTimeout(1500);

        console.log('URL after new doctor registration:', webPage.url());
        assert(webPage.url().includes('PendingApproval') || webPage.url().includes('pendingapproval'), 'Newly registered doctor is directed to Pending Approval page');

        const pendingHtml = await webPage.content();
        assert(pendingHtml.includes('Credentials Under Verification'), 'Pending page displays "Credentials Under Verification"');
        assert(pendingHtml.includes('Medical Council Registry Verification'), 'Pending page renders clinical verification timeline');

        await webPage.screenshot({ path: path.join(SCREENSHOT_DIR, 'group2_new_doctor_pending_approval.png') });
        console.log('📸 Captured group2_new_doctor_pending_approval.png');

        // -------------------------------------------------------------
        // 5. ADMIN APPROVAL WORKFLOW
        // -------------------------------------------------------------
        console.log('\n--- 5. Testing Admin Doctor Approval Workflow ---');
        await adminPage.goto(`${ADMIN_URL}/Auth/Login`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await adminPage.fill('#adminEmailInput', 'admin@ayurveda.com');
        await adminPage.fill('#adminPwdInput', getDevAdminPassword());
        await Promise.all([
            adminPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }).catch(e => {}),
            adminPage.click('button[type="submit"]')
        ]);
        await adminPage.waitForTimeout(1000);

        // Go to Pending Doctors list
        await adminPage.goto(`${ADMIN_URL}/Doctors/Pending`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await adminPage.waitForTimeout(1000);

        const adminPendingHtml = await adminPage.content();
        assert(adminPendingHtml.includes(testDoctorEmail), `Pending doctor ${testDoctorEmail} is listed in Admin pending queue`);

        // Click Approve on this doctor
        const approveForm = await adminPage.$(`tr:has-text("${testDoctorEmail}") form[action*="Approve"]`);
        if (approveForm) {
            const approveBtn = await approveForm.$('button[type="submit"]');
            await Promise.all([
                adminPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }),
                approveBtn.click()
            ]);
            console.log('Admin clicked Approve button for doctor.');
        }

        await adminPage.screenshot({ path: path.join(SCREENSHOT_DIR, 'group2_admin_approved_doctor.png') });
        console.log('📸 Captured group2_admin_approved_doctor.png');

        // -------------------------------------------------------------
        // 6. DOCTOR DASHBOARD AFTER APPROVAL
        // -------------------------------------------------------------
        console.log('\n--- 6. Verifying Doctor Accesses Dashboard After Admin Approval ---');
        await webPage.goto(`${WEB_URL}/DoctorPortal/Dashboard`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await webPage.waitForTimeout(1500);

        if (!webPage.url().includes('/DoctorPortal/Dashboard') && !webPage.url().includes('/doctorportal/dashboard')) {
            console.log('Session expired, logging in as approved doctor...');
            await webPage.goto(`${WEB_URL}/Account/Login?role=Doctor`, { waitUntil: 'domcontentloaded', timeout: 15000 });
            await webPage.fill('#loginForm input[name="Email"]', testDoctorEmail);
            await webPage.fill('#loginForm input[name="Password"]', 'DevDemoPass_2026!#');
            await Promise.all([
                webPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }).catch(e => {}),
                webPage.click('#loginForm button[type="submit"]')
            ]);
            await webPage.waitForTimeout(1500);
        }

        console.log('URL after approval check/login:', webPage.url());
        assert(webPage.url().includes('/DoctorPortal/Dashboard') || webPage.url().includes('/doctorportal/dashboard'), 'Approved doctor now reaches Clinical Dashboard');

        await webPage.screenshot({ path: path.join(SCREENSHOT_DIR, 'group2_doctor_dashboard_after_approval.png') });
        console.log('📸 Captured group2_doctor_dashboard_after_approval.png');

        // -------------------------------------------------------------
        // 7. REJECTED DOCTOR STATUS & REASON
        // -------------------------------------------------------------
        console.log('\n--- 7. Testing Rejected Doctor Message & Reason ---');
        const rejectEmail = `dr.rejected.${timestamp}@thegreatayurveda.com`;
        const rejectPhone = '986' + Math.floor(1000000 + Math.random() * 9000000);
        await webContext.clearCookies();

        await webPage.goto(`${WEB_URL}/doctor/register`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await webPage.fill('#wizardStep1 input[name="FullName"]', 'Dr. Reject Candidate');
        await webPage.fill('#wizardStep1 input[name="Email"]', rejectEmail);
        await webPage.fill('#wizardStep1 input[name="ContactNumber"]', rejectPhone);
        await webPage.fill('#wizardStep1 input[name="Password"]', 'DevDemoPass_2026!#');
        await webPage.fill('#wizardStep1 input[name="ConfirmPassword"]', 'DevDemoPass_2026!#');
        await webPage.click('#wizardStep1 button:has-text("Next: Professional Details")');
        await webPage.waitForTimeout(500);

        await webPage.selectOption('#wizardStep2 select[name="Qualification"]', 'BAMS');
        await webPage.fill('#wizardStep2 input[name="RegistrationNumber"]', `AYU-TEST-${timestamp.toString().slice(-4)}`);
        await webPage.fill('#wizardStep2 input[name="RegistrationCouncil"]', 'State Council');
        await webPage.fill('#wizardStep2 input[name="Languages"]', 'Hindi');
        await webPage.click('#wizardStep2 button:has-text("Next: Document Verification")');
        await webPage.waitForTimeout(500);

        await webPage.click('#wizardStep3 button:has-text("Next: Schedule & Slots")');
        await webPage.waitForTimeout(500);

        await webPage.click('#wizardStep4 button:has-text("Next: Review & Submit")');
        await webPage.waitForTimeout(500);

        await Promise.all([
            webPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }).catch(e => {}),
            webPage.click('#wizardStep5 button[type="submit"]')
        ]);
        await webPage.waitForTimeout(1500);

        // Admin rejects this doctor with reason
        await adminPage.goto(`${ADMIN_URL}/Doctors/Pending`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        const rejectBtn = await adminPage.$(`tr:has-text("${rejectEmail}") button[data-bs-target*="rejectModal"]`);
        if (rejectBtn) {
            await rejectBtn.click();
            await adminPage.waitForTimeout(500);
            const modalEl = await adminPage.$(`div.modal.show`);
            if (modalEl) {
                const reasonArea = await modalEl.$('textarea[name="rejectionReason"]');
                await reasonArea.fill('Incomplete council registration certificate provided. Please upload state council seal.');
                const confirmBtn = await modalEl.$('button[type="submit"]');
                await Promise.all([
                    adminPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }),
                    confirmBtn.click()
                ]);
                console.log('Admin submitted rejection with reason.');
            }
        }

        // Doctor reloads /DoctorPortal/PendingApproval
        await webPage.goto(`${WEB_URL}/DoctorPortal/PendingApproval`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await webPage.waitForTimeout(1000);

        const rejectedDoctorHtml = await webPage.content();
        assert(rejectedDoctorHtml.includes('Application Rejected'), 'Rejected doctor sees "Application Rejected" message (NOT pending)');
        assert(rejectedDoctorHtml.includes('Incomplete council registration certificate'), 'Rejected doctor sees the board rejection reason');

        await webPage.screenshot({ path: path.join(SCREENSHOT_DIR, 'group2_doctor_rejection_reason_view.png') });
        console.log('📸 Captured group2_doctor_rejection_reason_view.png');

        // -------------------------------------------------------------
        // 8. SUSPENDED DOCTOR STATUS & REASON
        // -------------------------------------------------------------
        console.log('\n--- 8. Testing Suspended Doctor Message & Reason ---');
        // Admin suspends the approved test doctor
        await adminPage.goto(`${ADMIN_URL}/Doctors`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        const doctorRow = await adminPage.$(`tr:has-text("${testDoctorEmail}")`);
        if (doctorRow) {
            // Open dropdown
            const dropdownBtn = await doctorRow.$('button[data-bs-toggle="dropdown"]');
            if (dropdownBtn) {
                await dropdownBtn.click();
                await adminPage.waitForTimeout(500);
                const suspendForm = await doctorRow.$('form[action*="Suspend"]');
                if (suspendForm) {
                    const suspendBtn = await suspendForm.$('button[type="submit"]');
                    await Promise.all([
                        adminPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }),
                        suspendBtn.click()
                    ]);
                    console.log('Admin suspended the test doctor.');
                }
            }
        }

        // Log in as the suspended doctor
        await webContext.clearCookies();
        await webPage.goto(`${WEB_URL}/Account/Login?role=Doctor`, { waitUntil: 'domcontentloaded', timeout: 15000 });
        await webPage.fill('#loginForm input[name="Email"]', testDoctorEmail);
        await webPage.fill('#loginForm input[name="Password"]', 'DevDemoPass_2026!#');
        await Promise.all([
            webPage.waitForNavigation({ waitUntil: 'domcontentloaded', timeout: 15000 }).catch(e => {}),
            webPage.click('#loginForm button[type="submit"]')
        ]);
        await webPage.waitForTimeout(1500);

        const suspendedDoctorHtml = await webPage.content();
        assert(suspendedDoctorHtml.includes('Practice Account Suspended'), 'Suspended doctor sees "Practice Account Suspended" message (NOT pending)');
        assert(suspendedDoctorHtml.includes('Reason for Suspension') || suspendedDoctorHtml.includes('compliance'), 'Suspended doctor sees suspension reason and compliance contact');

        await webPage.screenshot({ path: path.join(SCREENSHOT_DIR, 'group2_doctor_suspended_reason_view.png') });
        console.log('📸 Captured group2_doctor_suspended_reason_view.png');

        console.log('\n================================================================');
        console.log(`GROUP 2 SUMMARY: ${passedCount} PASSED, ${failedCount} FAILED`);
        console.log('================================================================');

    } catch (err) {
        console.error('Fatal error during test run:', err);
        failedCount++;
    } finally {
        await browser.close();
        process.exit(failedCount > 0 ? 1 : 0);
    }
}

run();
