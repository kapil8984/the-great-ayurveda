const { execSync } = require('child_process');
const fs = require('fs');

const patterns = [
    'Admin@123',
    'Doctor@123',
    'DoctorPass',
    'DevAuth',
    'SuperAdmin@2026!',
    'DoctorPass@2026',
    'AdminSecure@2026!',
    'DemoAdmin@2026!',
    'Customer@123',
    'Anand@123'
];

const files = execSync('git ls-files', { encoding: 'utf-8' })
    .split(/\r?\n/)
    .map(f => f.trim())
    .filter(f => f.length > 0 && fs.existsSync(f));

console.log(`Auditing ${files.length} tracked files for literal passwords...`);

let matchCount = 0;
const results = {};

for (const pattern of patterns) {
    results[pattern] = [];
    for (const file of files) {
        // Skip binary files
        if (file.endsWith('.jpg') || file.endsWith('.png') || file.endsWith('.webp') || file.endsWith('.ico') || file.endsWith('.woff2') || file.endsWith('.dll') || file.endsWith('.exe')) {
            continue;
        }
        try {
            const content = fs.readFileSync(file, 'utf-8');
            const lines = content.split(/\r?\n/);
            lines.forEach((line, idx) => {
                if (line.includes(pattern)) {
                    matchCount++;
                    results[pattern].push({ file, line: idx + 1, content: line.trim() });
                }
            });
        } catch (e) {
            // ignore binary / read error
        }
    }
}

console.log(`Total occurrences found in tracked files: ${matchCount}`);
for (const [pat, matches] of Object.entries(results)) {
    if (matches.length > 0) {
        console.log(`\n--- Pattern: "${pat}" (${matches.length} matches) ---`);
        for (const m of matches) {
            console.log(`  ${m.file}:${m.line} -> ${m.content}`);
        }
    } else {
        console.log(`Pattern "${pat}": 0 matches.`);
    }
}
