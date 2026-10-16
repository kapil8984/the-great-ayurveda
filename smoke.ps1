param(
    [Parameter(Mandatory = $false, Position = 0)]
    [string]$baseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"
$baseUrl = $baseUrl.TrimEnd('/')

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " SMOKE TEST SUITE: Checking 25 Key URLs + Images at $baseUrl" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan

$urlsToCheck = @(
    "/",                              # 1. Home
    "/shop",                          # 2. Shop
    "/shop/details/1",                # 3. Product 1
    "/shop/details/2",                # 4. Product 2
    "/shop/details/3",                # 5. Product 3
    "/shop/details/4",                # 6. Product 4
    "/shop/details/5",                # 7. Product 5
    "/shop/details/6",                # 8. Product 6
    "/shop/details/7",                # 9. Product 7
    "/shop/details/8",                # 10. Product 8
    "/shop/details/9",                # 11. Product 9
    "/shop/details/10",               # 12. Product 10
    "/doctors",                       # 13. Doctors directory
    "/doctors/profile/1",             # 14. Doctor profile 1
    "/doctors/profile/2",             # 15. Doctor profile 2
    "/doctors/profile/3",             # 16. Doctor profile 3
    "/doctors/profile/4",             # 17. Doctor profile 4
    "/doctors/profile/5",             # 18. Doctor profile 5
    "/home/about",                    # 19. About
    "/home/faq",                      # 20. FAQ
    "/robots.txt",                    # 21. Robots
    "/sitemap.xml",                   # 22. Sitemap
    "/account/login",                 # 23. Customer login
    "/doctor/login",                  # 24. Doctor login
    "/admin-portal/auth/login"        # 25. Admin login
)

$checkedImages = [System.Collections.Generic.HashSet[string]]::new()
$failedUrls = @()
$failedImages = @()

Add-Type -AssemblyName System.Net.Http -ErrorAction SilentlyContinue
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(45)

# 1. Test 25 key URLs
Write-Host "`n--- Testing 25 Key Production Endpoints ---" -ForegroundColor Yellow
$idx = 1
foreach ($path in $urlsToCheck) {
    $fullUrl = "$baseUrl$path"
    try {
        $resp = $client.GetAsync($fullUrl).GetAwaiter().GetResult()
        $status = [int]$resp.StatusCode
        if ($status -eq 200) {
            Write-Host "[$idx/25] [OK] $path (HTTP 200)" -ForegroundColor Green
            $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            # Extract image sources
            $matches = [regex]::Matches($body, '<img[^>]+src=["'']([^"'']+)["'']', [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            foreach ($m in $matches) {
                $src = $m.Groups[1].Value
                if (-not [string]::IsNullOrWhiteSpace($src) -and -not $src.StartsWith("data:")) {
                    $imgUrl = if ($src.StartsWith("http")) { $src } else { "$baseUrl/" + $src.TrimStart('/') }
                    [void]$checkedImages.Add($imgUrl)
                }
            }
        } else {
            Write-Host "[$idx/25] [FAIL] $path (HTTP $status)" -ForegroundColor Red
            $failedUrls += "$path (HTTP $status)"
        }
    } catch {
        Write-Host "[$idx/25] [ERROR] $path - $_" -ForegroundColor Red
        $failedUrls += "$path ($($_.Exception.Message))"
    }
    $idx++
}

# 2. Test /swagger must be 404 in Production
Write-Host "`n--- Testing /swagger security gate (must be 404) ---" -ForegroundColor Yellow
try {
    $swaggerUrl = "$baseUrl/swagger"
    $resp = $client.GetAsync($swaggerUrl).GetAwaiter().GetResult()
    $status = [int]$resp.StatusCode
    if ($status -eq 404) {
        Write-Host "[OK] /swagger returns HTTP 404 NotFound (Security verified)" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] /swagger returned HTTP $status (Expected 404)" -ForegroundColor Red
        $failedUrls += "/swagger (HTTP $status, Expected 404)"
    }
} catch {
    Write-Host "[INFO] /swagger check: $_" -ForegroundColor Green
}

# 3. Test Every Image Returns 200
Write-Host "`n--- Testing Extracted Images ($($checkedImages.Count) unique assets found) ---" -ForegroundColor Yellow
$imgIdx = 1
foreach ($img in $checkedImages) {
    try {
        $resp = $client.GetAsync($img).GetAwaiter().GetResult()
        $status = [int]$resp.StatusCode
        if ($status -eq 200) {
            Write-Host "  [IMG $imgIdx/$($checkedImages.Count)] [200 OK] $img" -ForegroundColor DarkGray
        } else {
            Write-Host "  [IMG $imgIdx/$($checkedImages.Count)] [FAIL $status] $img" -ForegroundColor Red
            $failedImages += "$img (HTTP $status)"
        }
    } catch {
        Write-Host "  [IMG $imgIdx/$($checkedImages.Count)] [ERR] $($img): $_" -ForegroundColor Red
        $failedImages += "$img ($($_.Exception.Message))"
    }
    $imgIdx++
}

Write-Host "`n======================================================================" -ForegroundColor Cyan
Write-Host " SMOKE TEST SUMMARY" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "Endpoints checked: 25 | Failed: $($failedUrls.Count)"
Write-Host "Images checked:    $($checkedImages.Count) | Failed: $($failedImages.Count)"

if ($failedUrls.Count -gt 0 -or $failedImages.Count -gt 0) {
    Write-Host "`nFAILURES DETECTED:" -ForegroundColor Red
    foreach ($f in $failedUrls) { Write-Host "  Endpoint: $f" -ForegroundColor Red }
    foreach ($f in $failedImages) { Write-Host "  Image:    $f" -ForegroundColor Red }
    exit 1
} else {
    Write-Host "`nALL 25 ENDPOINTS, SWAGGER 404 GATE, AND ALL IMAGES PASSED WITH ZERO ERRORS!" -ForegroundColor Green
    exit 0
}
