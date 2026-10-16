const fs = require('fs');
const path = require('path');
const https = require('https');

const fontsDir = path.resolve(__dirname, '../src/Ayurveda.Web/wwwroot/fonts');
fs.mkdirSync(fontsDir, { recursive: true });

const fonts = [
  {
    file: 'cormorant-garamond-600.woff2',
    url: 'https://fonts.gstatic.com/s/cormorantgaramond/v21/co3bmX5slCNuHLi8bLeY9MK7whWMhyjYqXtK.woff2'
  },
  {
    file: 'cormorant-garamond-700.woff2',
    url: 'https://fonts.gstatic.com/s/cormorantgaramond/v21/co3bmX5slCNuHLi8bLeY9MK7whWMhyjYqXtK.woff2'
  },
  {
    file: 'plus-jakarta-sans-400.woff2',
    url: 'https://fonts.gstatic.com/s/plusjakartasans/v12/LDIoaomQNQcsA88c7O9yZ4KMCoOg4Ko20yw.woff2'
  },
  {
    file: 'plus-jakarta-sans-500.woff2',
    url: 'https://fonts.gstatic.com/s/plusjakartasans/v12/LDIoaomQNQcsA88c7O9yZ4KMCoOg4Ko20yw.woff2'
  },
  {
    file: 'plus-jakarta-sans-600.woff2',
    url: 'https://fonts.gstatic.com/s/plusjakartasans/v12/LDIoaomQNQcsA88c7O9yZ4KMCoOg4Ko20yw.woff2'
  },
  {
    file: 'plus-jakarta-sans-700.woff2',
    url: 'https://fonts.gstatic.com/s/plusjakartasans/v12/LDIoaomQNQcsA88c7O9yZ4KMCoOg4Ko20yw.woff2'
  }
];

function download(item) {
  return new Promise((resolve, reject) => {
    const dest = path.join(fontsDir, item.file);
    const file = fs.createWriteStream(dest);
    https.get(item.url, res => {
      if (res.statusCode !== 200) {
        return reject(new Error(`Failed to download ${item.url}: status ${res.statusCode}`));
      }
      res.pipe(file);
      file.on('finish', () => {
        file.close(() => {
          console.log(`Saved ${item.file}`);
          resolve();
        });
      });
    }).on('error', err => {
      fs.unlink(dest, () => {});
      reject(err);
    });
  });
}

async function run() {
  for (const f of fonts) {
    try {
      await download(f);
    } catch (e) {
      console.warn(`Font download warning: ${e.message}`);
    }
  }
  console.log('Fonts download process finished.');
}

run();
