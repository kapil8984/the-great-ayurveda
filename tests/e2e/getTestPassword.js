const fs = require('fs');
const path = require('path');

function getDevAdminPassword() {
    if (process.env.DEV_ADMIN_PASSWORD) {
        return process.env.DEV_ADMIN_PASSWORD;
    }
    const devPassPath = path.resolve(__dirname, '../../App_Data/dev_demo_password.txt');
    if (fs.existsSync(devPassPath)) {
        return fs.readFileSync(devPassPath, 'utf8').trim();
    }
    return '';
}

module.exports = { getDevAdminPassword };
