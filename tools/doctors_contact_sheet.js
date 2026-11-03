// ==============================================================================
// tools/doctors_contact_sheet.js
// Terminal Contact Sheet & Perceptual Hash Auditor for 24 Clinical Vaidyas
// ==============================================================================
const fs = require('fs');
const path = require('path');
const sharp = require('../tests/e2e/node_modules/sharp');

const DOCTORS_DIR = path.join(__dirname, '..', 'src', 'Ayurveda.Web', 'wwwroot', 'images', 'doctors');

const DOCTOR_METADATA = [
    { id: 1, name: "Dr. Aarav Sharma", gender: "Male", exp: "16y", spec: "Kayachikitsa / Digestive Care", file: "doctor-1.webp" },
    { id: 2, name: "Dr. Ananya Nambiar", gender: "Female", exp: "14y", spec: "Prasuti & Stri Roga (Women's)", file: "doctor-2.webp" },
    { id: 3, name: "Dr. Vikramaditya Joshi", gender: "Male", exp: "24y", spec: "Panchakarma & Therapy Kits", file: "doctor-3.webp" },
    { id: 4, name: "Dr. Meera Deshmukh", gender: "Female", exp: "8y", spec: "Ayurvedic Cosmetology / Skin", file: "doctor-4.webp" },
    { id: 5, name: "Dr. Rajeshwari Iyer", gender: "Female", exp: "18y", spec: "Kaumarbhritya / Pediatrics", file: "doctor-5.webp" },
    { id: 6, name: "Dr. Harish C. Shastri", gender: "Male", exp: "28y", spec: "Sandhivata / Joint & Muscle", file: "doctor-6.webp" },
    { id: 7, name: "Dr. Tanvi Bhatia", gender: "Female", exp: "6y", spec: "Stress, Sleep & Brain", file: "doctor-7.webp" },
    { id: 8, name: "Dr. Devendra Hegde", gender: "Male", exp: "15y", spec: "Dravyaguna / Botanical Taxonomy", file: "doctor-8.webp" },
    { id: 9, name: "Dr. Suniti Mukherjee", gender: "Female", exp: "11y", spec: "Diabetes & Metabolism Support", file: "doctor-9.webp" },
    { id: 10, name: "Dr. Pradeep Varma", gender: "Male", exp: "19y", spec: "Shalakya Tantra (ENT / Eye)", file: "doctor-10.webp" },
    { id: 11, name: "Dr. Shalini Kulkarni", gender: "Female", exp: "5y", spec: "Trichology / Hair Care", file: "doctor-11.webp" },
    { id: 12, name: "Dr. Balram Upadhyay", gender: "Male", exp: "22y", spec: "Shwasa-Kasa / Respiratory", file: "doctor-12.webp" },
    { id: 13, name: "Dr. Kavita Patel", gender: "Female", exp: "9y", spec: "Digestive Agni / Gut Health", file: "doctor-13.webp" },
    { id: 14, name: "Dr. Somnath Sen", gender: "Male", exp: "20y", spec: "Rasashastra & Ghritha Tonic", file: "doctor-14.webp" },
    { id: 15, name: "Dr. Priyamvada Rao", gender: "Female", exp: "7y", spec: "Hormonal Balance & Shatavari", file: "doctor-15.webp" },
    { id: 16, name: "Dr. Madhavan Pillai", gender: "Male", exp: "26y", spec: "Marma Chikitsa / Spine Care", file: "doctor-16.webp" },
    { id: 17, name: "Dr. Alok Kumar Tiwari", gender: "Male", exp: "12y", spec: "Manasa Roga / Mind & Sleep", file: "doctor-17.webp" },
    { id: 18, name: "Dr. Rituja Chawla", gender: "Female", exp: "10y", spec: "Twak Roga / Skin Glow & Detox", file: "doctor-18.webp" },
    { id: 19, name: "Dr. Eknath Gaikwad", gender: "Male", exp: "17y", spec: "Metabolic Syndrome & Liver", file: "doctor-19.webp" },
    { id: 20, name: "Dr. Jayashree Sundaram", gender: "Female", exp: "13y", spec: "AyurYoga & Postural Therapy", file: "doctor-20.webp" },
    { id: 21, name: "Dr. Gaurav Aggarwal", gender: "Male", exp: "4y", spec: "Scalp Health & Hair Fall", file: "doctor-21.webp" },
    { id: 22, name: "Dr. Vasantha Kumari", gender: "Female", exp: "25y", spec: "Jara Chikitsa / Geriatrics", file: "doctor-22.webp" },
    { id: 23, name: "Dr. Nikhilesh Roy", gender: "Male", exp: "3y", spec: "General Consultation / Ahara", file: "doctor-23.webp" },
    { id: 24, name: "Dr. Sarojini Nair", gender: "Female", exp: "30y", spec: "Panchakarma / Gold Medalist", file: "doctor-24.webp" },
];

async function computeDHash(imagePath) {
    const rawData = await sharp(imagePath)
        .resize(9, 8, { fit: 'fill' })
        .grayscale()
        .raw()
        .toBuffer();

    let hash = '';
    for (let row = 0; row < 8; row++) {
        for (let col = 0; col < 8; col++) {
            const left = rawData[row * 9 + col];
            const right = rawData[row * 9 + col + 1];
            hash += (left > right ? '1' : '0');
        }
    }
    // Convert 64-bit binary string to 16-char hex
    let hexHash = '';
    for (let i = 0; i < 64; i += 4) {
        hexHash += parseInt(hash.slice(i, i + 4), 2).toString(16);
    }
    return { bin: hash, hex: hexHash };
}

function hammingDistance(h1, h2) {
    let dist = 0;
    for (let i = 0; i < h1.length; i++) {
        if (h1[i] !== h2[i]) dist++;
    }
    return dist;
}

async function main() {
    console.log('='.repeat(108));
    console.log('  THE GREAT AYURVEDA — CLINICAL VAIDYAS CONTACT SHEET (24 FICTIONAL DOCTORS)');
    console.log('  Photorealistic Fictional Portraits • 400x400 WebP • Perceptual Hash (dHash) Audit');
    console.log('='.repeat(108));

    const rows = [];
    const hashList = [];

    for (const doc of DOCTOR_METADATA) {
        const filePath = path.join(DOCTORS_DIR, doc.file);
        if (!fs.existsSync(filePath)) {
            console.error(`ERROR: File missing: ${filePath}`);
            process.exit(1);
        }

        const stat = fs.statSync(filePath);
        const meta = await sharp(filePath).metadata();
        const hashObj = await computeDHash(filePath);

        const sizeKb = (stat.size / 1024).toFixed(1);
        const isUnder40Kb = stat.size <= 40 * 1024;
        const isExact400 = meta.width === 400 && meta.height === 400;
        const isWebp = meta.format === 'webp';

        hashList.push({ id: doc.id, name: doc.name, file: doc.file, hash: hashObj.bin, hex: hashObj.hex });

        rows.push({
            id: doc.id.toString().padStart(2, ' '),
            file: doc.file.padEnd(14, ' '),
            name: doc.name.padEnd(24, ' '),
            demographics: `${doc.gender}, ${doc.exp}`.padEnd(12, ' '),
            spec: doc.spec.padEnd(32, ' '),
            dims: `${meta.width}x${meta.height}`.padEnd(8, ' '),
            size: `${sizeKb} KB`.padStart(8, ' '),
            hash: hashObj.hex,
            status: (isUnder40Kb && isExact400 && isWebp) ? 'PASS' : 'FAIL'
        });
    }

    // Print ASCII Contact Sheet Table
    console.log('\n┌────┬──────────────┬────────────────────────┬────────────┬────────────────────────────────┬────────┬──────────┬──────────────────┬────────┐');
    console.log('│ ID │ Filename     │ Doctor Name            │ Profile    │ Specialization                 │ Dims   │ Size     │ dHash (Hex 64b)  │ Status │');
    console.log('├────┼──────────────┼────────────────────────┼────────────┼────────────────────────────────┼────────┼──────────┼──────────────────┼────────┤');

    for (const r of rows) {
        console.log(`│ ${r.id} │ ${r.file} │ ${r.name} │ ${r.demographics} │ ${r.spec} │ ${r.dims} │ ${r.size} │ ${r.hash} │  ${r.status}  │`);
    }

    console.log('└────┴──────────────┴────────────────────────┴────────────┴────────────────────────────────┴────────┴──────────┴──────────────────┴────────┘');

    // Duplicate Check using Hamming Distance
    console.log('\n--- Perceptual Hash Duplicate Audit (Hamming Distance >= 10 Required) ---');
    let minDistance = 64;
    let minPair = null;
    const failingPairs = [];

    for (let i = 0; i < hashList.length; i++) {
        for (let j = i + 1; j < hashList.length; j++) {
            const dist = hammingDistance(hashList[i].hash, hashList[j].hash);
            if (dist < minDistance) {
                minDistance = dist;
                minPair = { docA: hashList[i], docB: hashList[j], distance: dist };
            }
            if (dist < 10) {
                failingPairs.push({
                    docA: hashList[i],
                    docB: hashList[j],
                    distance: dist
                });
            }
        }
    }

    console.log(`Audited all ${hashList.length * (hashList.length - 1) / 2} pairwise combinations.`);
    console.log(`Minimum observed Hamming Distance: ${minDistance} (between ${minPair?.docA.file} and ${minPair?.docB.file})`);

    if (failingPairs.length === 0) {
        console.log(`✅ PASS: All 24 portraits meet the strict distinctness threshold (all pairwise Hamming distances >= 10).`);
        console.log(`✅ Min Distance = ${minDistance} >= 10.`);
    } else {
        console.warn(`⚠️ FAILING PAIRS (Hamming distance < 10): ${failingPairs.length} pairs`);
        for (const p of failingPairs) {
            console.warn(`   - ${p.docA.file} (${p.docA.name}) <-> ${p.docB.file} (${p.docB.name}): distance = ${p.distance}`);
        }
    }
    console.log('='.repeat(108));
}

main().catch(err => {
    console.error('Fatal error:', err);
    process.exit(1);
});
