const http = require('http');
const { URL } = require('url');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:5099';

function fetchUrl(urlStr, maxRedirects = 3) {
    return new Promise((resolve, reject) => {
        const u = new URL(urlStr);
        const req = http.get(u, (res) => {
            if ((res.statusCode === 301 || res.statusCode === 302 || res.statusCode === 307 || res.statusCode === 308) && res.headers.location && maxRedirects > 0) {
                const redirectUrl = new URL(res.headers.location, urlStr).href;
                return resolve(fetchUrl(redirectUrl, maxRedirects - 1));
            }
            let data = '';
            res.on('data', chunk => data += chunk);
            res.on('end', () => {
                resolve({ statusCode: res.statusCode, headers: res.headers, body: data, url: urlStr });
            });
        });
        req.on('error', reject);
        req.setTimeout(15000, () => {
            req.destroy();
            reject(new Error(`Timeout fetching ${urlStr}`));
        });
    });
}

(async () => {
    console.log('======================================================================');
    console.log(' SECTION F: SITEMAP & ALL-IMAGE CRAWL');
    console.log(` Target Server: ${BASE_URL}`);
    console.log('======================================================================\n');

    // 1. Fetch /sitemap.xml
    const sitemapUrl = `${BASE_URL}/sitemap.xml`;
    console.log(`Fetching sitemap from: ${sitemapUrl}...`);
    const sitemapRes = await fetchUrl(sitemapUrl);
    if (sitemapRes.statusCode !== 200) {
        console.error(`FAILED to fetch sitemap.xml. Status: ${sitemapRes.statusCode}`);
        process.exit(1);
    }

    const locMatches = [...sitemapRes.body.matchAll(/<loc>(.*?)<\/loc>/g)].map(m => m[1].trim());
    console.log(`Found ${locMatches.length} URLs in /sitemap.xml`);

    // Standard core pages + all sitemap URLs
    const pagesToCrawl = new Set([
        `${BASE_URL}/`,
        `${BASE_URL}/shop`,
        `${BASE_URL}/doctors`,
        `${BASE_URL}/home/privacy`,
        `${BASE_URL}/home/terms`,
        `${BASE_URL}/home/shipping`,
        `${BASE_URL}/home/refunds`,
        `${BASE_URL}/shop/details/1`,
        `${BASE_URL}/account/login`,
        ...locMatches.map(u => u.replace(/^https?:\/\/[^\/]+/, BASE_URL))
    ]);

    console.log(`Unique pages to crawl: ${pagesToCrawl.size}`);

    const allImages = new Set();
    let brokenPages = 0;
    let placeholderReferences = 0;

    let crawledPageCount = 0;
    for (const pageUrl of pagesToCrawl) {
        try {
            const res = await fetchUrl(pageUrl);
            crawledPageCount++;
            if (res.statusCode !== 200) {
                console.error(`[FAIL] Page returned non-200: ${pageUrl} (status: ${res.statusCode})`);
                brokenPages++;
                continue;
            }

            // Extract all img src
            const imgRegex = /<img[^>]+src=["']([^"']+)["']/gi;
            let match;
            while ((match = imgRegex.exec(res.body)) !== null) {
                let src = match[1];
                if (src.includes('placeholder.svg')) {
                    console.error(`[VIOLATION] placeholder.svg referenced on page: ${pageUrl}`);
                    placeholderReferences++;
                }

                // Exclude data: URLs or external icons if any
                if (src.startsWith('data:')) continue;
                if (src.startsWith('http://') || src.startsWith('https://')) {
                    if (src.includes('localhost') || src.includes('127.0.0.1')) {
                        allImages.add(src);
                    }
                } else {
                    const resolved = new URL(src, pageUrl).href;
                    allImages.add(resolved);
                }
            }
        } catch (err) {
            console.error(`[ERROR] Crawling page ${pageUrl}: ${err.message}`);
            brokenPages++;
        }
    }

    console.log(`\nSuccessfully crawled ${crawledPageCount} pages.`);
    console.log(`Discovered ${allImages.size} unique image assets. Verifying image HTTP status codes...`);

    let checkedImages = 0;
    let brokenImages = 0;

    for (const imgUrl of allImages) {
        try {
            const res = await fetchUrl(imgUrl);
            checkedImages++;
            if (res.statusCode !== 200) {
                console.error(`[BROKEN IMAGE] Status ${res.statusCode}: ${imgUrl}`);
                brokenImages++;
            }
        } catch (err) {
            console.error(`[IMAGE ERROR] ${imgUrl}: ${err.message}`);
            brokenImages++;
        }
    }

    console.log('\n======================================================================');
    console.log(' SITEMAP & IMAGE CRAWL RESULTS:');
    console.log(` Total Pages Crawled: ${crawledPageCount}`);
    console.log(` Broken Pages: ${brokenPages}`);
    console.log(` Total Unique Images Checked: ${checkedImages}`);
    console.log(` Broken Images (Non-200): ${brokenImages}`);
    console.log(` Placeholder Images (placeholder.svg): ${placeholderReferences}`);
    console.log('======================================================================\n');

    if (brokenPages > 0 || brokenImages > 0 || placeholderReferences > 0) {
        console.error('VERIFICATION FAILED!');
        process.exit(1);
    } else {
        console.log('ALL VERIFICATIONS PASSED: 0 broken pages, 0 broken images, 0 placeholders!');
    }
})();
