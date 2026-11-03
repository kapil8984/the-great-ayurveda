const { execSync } = require('child_process');

const patterns = [
    'Admin@123',
    'Doctor@123',
    'DoctorPass',
    'DevAuth',
    'SuperAdmin@2026',
    'Customer@123',
    'Anand@123',
    'AdminSecure@2026',
    'DoctorPass@2026'
];

for (const p of patterns) {
    console.log(`=== Pattern: ${p} ===`);
    try {
        const out = execSync(`git grep -n "${p}"`, { encoding: 'utf8' });
        console.log(out.trim());
    } catch (e) {
        console.log('No tracked matches');
    }
}
