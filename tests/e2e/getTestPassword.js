const fs = require('fs');
const path = require('path');

function getCredentials() {
    const credsPath = path.resolve(__dirname, '../../App_Data/test_credentials.json');
    if (fs.existsSync(credsPath)) {
        try {
            return JSON.parse(fs.readFileSync(credsPath, 'utf8'));
        } catch (e) {}
    }
    return {};
}

function getDevAdminPassword() {
    if (process.env.DEV_ADMIN_PASSWORD) {
        return process.env.DEV_ADMIN_PASSWORD;
    }
    const creds = getCredentials();
    if (creds.AdminPassword) {
        return creds.AdminPassword;
    }
    const devPassPath = path.resolve(__dirname, '../../App_Data/dev_demo_password.txt');
    if (fs.existsSync(devPassPath)) {
        return fs.readFileSync(devPassPath, 'utf8').trim();
    }
    return '';
}

function getAdminPassword() {
    return process.env.ADMIN_PASSWORD || process.env.Admin__InitialPassword || getCredentials().AdminPassword || getDevAdminPassword();
}

function getDemoAdminPassword() {
    return process.env.DEMO_ADMIN_PASSWORD || getCredentials().DemoAdminPassword || '';
}

function getCustomerPassword() {
    return process.env.CUSTOMER_PASSWORD || getCredentials().CustomerPassword || '';
}

function getDoctorPassword() {
    return process.env.DOCTOR_PASSWORD || getCredentials().DoctorPassword || '';
}

module.exports = {
    getDevAdminPassword,
    getAdminPassword,
    getDemoAdminPassword,
    getCustomerPassword,
    getDoctorPassword
};
