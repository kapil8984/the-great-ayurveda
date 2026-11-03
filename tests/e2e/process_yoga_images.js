const fs = require('fs');
const path = require('path');
const sharp = require('sharp');

const brainDir = 'C:\\Users\\HP\\.gemini\\antigravity-ide\\brain\\4e2d7980-3744-4de0-bb20-91628ef25197';
const outputDir = path.resolve(__dirname, '../../src/Ayurveda.Web/wwwroot/images/yoga');
const stepsDir = path.join(outputDir, 'steps');

if (!fs.existsSync(outputDir)) fs.mkdirSync(outputDir, { recursive: true });
if (!fs.existsSync(stepsDir)) fs.mkdirSync(stepsDir, { recursive: true });

const asanas = [
  { slug: 'surya-namaskar', file: 'yoga_surya_namaskar_1793077266437.jpg', steps: 6 },
  { slug: 'tadasana', file: 'yoga_tadasana_1793077278742.jpg', steps: 5 },
  { slug: 'vrikshasana', file: 'yoga_vrikshasana_1793077290812.jpg', steps: 5 },
  { slug: 'trikonasana', file: 'yoga_trikonasana_1793077312564.jpg', steps: 5 },
  { slug: 'bhujangasana', file: 'yoga_bhujangasana_1793077376086.jpg', steps: 5 },
  { slug: 'dhanurasana', file: 'yoga_dhanurasana_1793077388633.jpg', steps: 5 },
  { slug: 'vajrasana', file: 'yoga_vajrasana_1793077406319.jpg', steps: 5 },
  { slug: 'padmasana', file: 'yoga_padmasana_1793077421838.jpg', steps: 5 },
  { slug: 'ardha-matsyendrasana', file: 'yoga_ardha_matsyendra_1793077448945.jpg', steps: 5 },
  { slug: 'paschimottanasana', file: 'yoga_paschimottana_1793077465503.jpg', steps: 5 },
  { slug: 'setu-bandhasana', file: 'yoga_setu_bandhasana_1793077484054.jpg', steps: 5 },
  { slug: 'shavasana', file: 'yoga_shavasana_1793077497027.jpg', steps: 5 }
];

async function run() {
  console.log('Processing Yoga images...');
  for (const asana of asanas) {
    const inputPath = path.join(brainDir, asana.file);
    if (!fs.existsSync(inputPath)) {
      console.error(`Missing input file: ${inputPath}`);
      continue;
    }

    const mainOut = path.join(outputDir, `${asana.slug}.webp`);
    // Resize main image to 600x450 WebP with quality 80
    await sharp(inputPath)
      .resize(600, 450, { fit: 'cover' })
      .webp({ quality: 80, effort: 5 })
      .toFile(mainOut);

    const mainStat = fs.statSync(mainOut);
    console.log(`[MAIN] ${asana.slug}.webp -> ${(mainStat.size / 1024).toFixed(1)} KB`);
    if (mainStat.size > 40 * 1024) {
      console.warn(`WARNING: ${asana.slug}.webp exceeds 40KB! Re-compressing...`);
      await sharp(inputPath)
        .resize(600, 450, { fit: 'cover' })
        .webp({ quality: 70, effort: 6 })
        .toFile(mainOut);
    }

    // Process step images
    const metadata = await sharp(inputPath).metadata();
    const width = metadata.width;
    const height = metadata.height;

    for (let i = 1; i <= asana.steps; i++) {
      const stepOut = path.join(stepsDir, `${asana.slug}-step-${i}.webp`);
      // Create slight variations/crops for the step illustrations
      // Step 1: full center slightly zoomed
      // Step 2: focus on upper body / arms
      // Step 3: focus on lower body / base
      // Step 4: apex wide angle
      // Step 5: close focus
      // Step 6: full serene hold
      let cropExtract = null;
      if (i === 1) {
        cropExtract = { left: Math.round(width * 0.05), top: Math.round(height * 0.05), width: Math.round(width * 0.9), height: Math.round(height * 0.9) };
      } else if (i === 2) {
        cropExtract = { left: Math.round(width * 0.1), top: Math.round(height * 0.0), width: Math.round(width * 0.8), height: Math.round(height * 0.75) };
      } else if (i === 3) {
        cropExtract = { left: Math.round(width * 0.1), top: Math.round(height * 0.2), width: Math.round(width * 0.8), height: Math.round(height * 0.75) };
      } else if (i === 4) {
        cropExtract = { left: Math.round(width * 0.02), top: Math.round(height * 0.02), width: Math.round(width * 0.96), height: Math.round(height * 0.96) };
      } else if (i === 5) {
        cropExtract = { left: Math.round(width * 0.15), top: Math.round(height * 0.1), width: Math.round(width * 0.7), height: Math.round(height * 0.7) };
      } else {
        cropExtract = { left: 0, top: 0, width: width, height: height };
      }

      await sharp(inputPath)
        .extract(cropExtract)
        .resize(480, 360, { fit: 'cover' })
        .webp({ quality: 78, effort: 5 })
        .toFile(stepOut);

      const stepStat = fs.statSync(stepOut);
      if (stepStat.size > 40 * 1024) {
        await sharp(inputPath)
          .extract(cropExtract)
          .resize(480, 360, { fit: 'cover' })
          .webp({ quality: 68, effort: 6 })
          .toFile(stepOut);
      }
    }
  }

  console.log('All yoga images and steps processed.');
}

run().catch(err => console.error(err));
