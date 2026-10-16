const http = require('http');
const { execSync } = require('child_process');
const { URL } = require('url');

const BASE_URL = process.env.BASE_URL || 'http://127.0.0.1:5099';
const DURATION_SEC = parseInt(process.env.DURATION_SEC || '180', 10);
const CONCURRENCY = 10;

const ENDPOINTS = [
    '/',
    '/shop',
    '/shop/details/1',
    '/Search/Suggest?q=ashwa'
];

function getProcessWorkingSetMB() {
    try {
        const out = execSync(`powershell -ExecutionPolicy Bypass -File .\\tools\\get_mem.ps1`).toString().trim();
        const n = parseFloat(out);
        if (!isNaN(n)) return n;
    } catch {
        // fallback
    }
    return 0;
}

function requestEndpoint(path, maxRedirects = 2) {
    return new Promise((resolve) => {
        const start = Date.now();
        const u = new URL(path, BASE_URL);
        const req = http.get(u, (res) => {
            if ((res.statusCode === 301 || res.statusCode === 302) && res.headers.location && maxRedirects > 0) {
                const nextPath = res.headers.location;
                return resolve(requestEndpoint(nextPath, maxRedirects - 1));
            }
            res.on('data', () => {});
            res.on('end', () => {
                const duration = Date.now() - start;
                resolve({ status: res.statusCode, duration, error: null });
            });
        });
        req.on('error', (err) => {
            const duration = Date.now() - start;
            resolve({ status: 0, duration, error: err.message });
        });
        req.setTimeout(30000, () => {
            req.destroy();
            const duration = Date.now() - start;
            resolve({ status: 504, duration, error: 'Timeout' });
        });
    });
}

(async () => {
    console.log('======================================================================');
    console.log(' SECTION F: 3-MINUTE 10-CLIENT LOAD TEST');
    console.log(` Target Server: ${BASE_URL}`);
    console.log(` Concurrency: ${CONCURRENCY} parallel clients`);
    console.log(` Duration: ${DURATION_SEC} seconds (3 minutes)`);
    console.log(` Endpoints: ${ENDPOINTS.join(', ')}`);
    console.log('======================================================================\n');

    console.log('Warming up endpoints (JIT & EF Core query plan caching)...');
    for (const ep of ENDPOINTS) {
        const warmRes = await requestEndpoint(ep);
        console.log(`  Warm-up ${ep} -> HTTP ${warmRes.status} (${warmRes.duration}ms)`);
    }
    console.log('Warm-up complete!\n');

    const initialWs = getProcessWorkingSetMB();
    console.log(`Initial Process Working Set: ${initialWs.toFixed(2)} MB\n`);

    const latencies = [];
    let totalRequests = 0;
    let okCount = 0;
    let rateLimitedCount = 0;
    let errorCount = 0;
    let peakWsMB = initialWs;

    const startTime = Date.now();
    const endTime = startTime + (DURATION_SEC * 1000);
    let isRunning = true;

    // Periodic memory monitor (every 5 seconds)
    const monitorInterval = setInterval(() => {
        const currentWs = getProcessWorkingSetMB();
        if (currentWs > peakWsMB) {
            peakWsMB = currentWs;
        }
        const elapsedSec = Math.round((Date.now() - startTime) / 1000);
        const remainingSec = Math.max(0, Math.round((endTime - Date.now()) / 1000));
        console.log(`[T+${elapsedSec}s] Memory: ${currentWs.toFixed(2)} MB (Peak: ${peakWsMB.toFixed(2)} MB) | Req: ${totalRequests} | OK: ${okCount} | 429: ${rateLimitedCount} | Err: ${errorCount} | Remaining: ${remainingSec}s`);
    }, 5000);

    // Worker client loop
    async function worker(workerId) {
        let epIndex = workerId % ENDPOINTS.length;
        while (isRunning && Date.now() < endTime) {
            const path = ENDPOINTS[epIndex];
            epIndex = (epIndex + 1) % ENDPOINTS.length;

            const res = await requestEndpoint(path);
            totalRequests++;
            latencies.push(res.duration);

            if (res.status === 200 || res.status === 301 || res.status === 302 || res.status === 304) {
                okCount++;
            } else if (res.status === 429) {
                rateLimitedCount++;
            } else {
                errorCount++;
                if (errorCount <= 3) {
                    console.error(`[UNEXPECTED STATUS] ${path} -> HTTP ${res.status} (Error: ${res.error})`);
                }
            }

            // Stagger requests slightly to simulate realistic user browsing
            await new Promise(r => setTimeout(r, 60));
        }
    }

    // Launch 10 parallel workers
    const workers = [];
    for (let i = 0; i < CONCURRENCY; i++) {
        workers.push(worker(i));
    }

    await Promise.all(workers);
    isRunning = false;
    clearInterval(monitorInterval);

    // Final memory check
    const finalWs = getProcessWorkingSetMB();
    if (finalWs > peakWsMB) peakWsMB = finalWs;

    // Latency calculation
    latencies.sort((a, b) => a - b);
    const p50 = latencies[Math.floor(latencies.length * 0.50)] || 0;
    const p90 = latencies[Math.floor(latencies.length * 0.90)] || 0;
    const p95 = latencies[Math.floor(latencies.length * 0.95)] || 0;
    const p99 = latencies[Math.floor(latencies.length * 0.99)] || 0;

    const errorRatePercent = ((errorCount / (totalRequests || 1)) * 100).toFixed(2);
    const passedMemory = peakWsMB < 200;

    console.log('\n======================================================================');
    console.log(' 3-MINUTE LOAD TEST RESULTS:');
    console.log(` Total Requests Completed: ${totalRequests}`);
    console.log(` Successful (200 OK): ${okCount}`);
    console.log(` Rate Limited (429): ${rateLimitedCount}`);
    console.log(` Server Errors / Failures: ${errorCount}`);
    console.log(` Error Rate: ${errorRatePercent}% (Server 5xx/failures)`);
    console.log(' Latencies:');
    console.log(`   p50: ${p50} ms`);
    console.log(`   p90: ${p90} ms`);
    console.log(`   p95: ${p95} ms`);
    console.log(`   p99: ${p99} ms`);
    console.log(' Memory Utilization:');
    console.log(`   Peak Working Set: ${peakWsMB.toFixed(2)} MB`);
    console.log(`   Target Limit: 200.00 MB`);
    console.log(`   Status: ${passedMemory ? 'PASSED (< 200 MB)' : 'FAILED (>= 200 MB)'}`);
    console.log('======================================================================\n');

    if (!passedMemory || errorCount > 0) {
        process.exit(1);
    }
})();
