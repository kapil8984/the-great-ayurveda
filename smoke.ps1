param(
    [Parameter(Mandatory = $false, Position = 0)]
    [string]$baseUrl = "http://localhost:5000"
)

$ErrorActionPreference = "Stop"
$baseUrl = $baseUrl.TrimEnd('/')

Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host " SMOKE TEST SUITE: 25 Key URLs + Static Assets + Images at $baseUrl" -ForegroundColor Cyan
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

$checkedImages   = [System.Collections.Generic.HashSet[string]]::new()
$homePageImgSet  = [System.Collections.Generic.HashSet[string]]::new()
$failedUrls      = @()
$failedImages    = @()

Add-Type -AssemblyName System.Net.Http -ErrorAction SilentlyContinue
$handler = [System.Net.Http.HttpClientHandler]::new()
$handler.AllowAutoRedirect = $false
$client = [System.Net.Http.HttpClient]::new($handler)
$client.Timeout = [TimeSpan]::FromSeconds(45)

# ============================================================
# 1. Test 25 key URLs
# ============================================================
Write-Host "`n--- Testing 25 Key Production Endpoints ---" -ForegroundColor Yellow
$idx = 1
foreach ($path in $urlsToCheck) {
    $fullUrl = "$baseUrl$path"
    try {
        $resp   = $client.GetAsync($fullUrl).GetAwaiter().GetResult()
        $status = [int]$resp.StatusCode
        if ($status -eq 200) {
            Write-Host "[$idx/25] [OK] $path (HTTP 200)" -ForegroundColor Green
            $body = $resp.Content.ReadAsStringAsync().GetAwaiter().GetResult()
            $rxMatches = [regex]::Matches($body, '<img[^>]+src=["'']([^"'']+)["'']', `
                [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
            foreach ($m in $rxMatches) {
                $src = $m.Groups[1].Value
                if (-not [string]::IsNullOrWhiteSpace($src) -and -not $src.StartsWith("data:")) {
                    $imgUrl = if ($src.StartsWith("http")) { $src } else { "$baseUrl/" + $src.TrimStart('/') }
                    [void]$checkedImages.Add($imgUrl)
                    if ($path -eq "/") { [void]$homePageImgSet.Add($imgUrl) }
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

# ============================================================
# 2. /swagger must be 404 in Production
# ============================================================
Write-Host "`n--- Testing /swagger security gate (must be 404) ---" -ForegroundColor Yellow
try {
    $resp   = $client.GetAsync("$baseUrl/swagger").GetAwaiter().GetResult()
    $status = [int]$resp.StatusCode
    if ($status -eq 404) {
        Write-Host "[OK] /swagger returns HTTP 404 (Security verified)" -ForegroundColor Green
    } else {
        Write-Host "[FAIL] /swagger returned HTTP $status (Expected 404)" -ForegroundColor Red
        $failedUrls += "/swagger (HTTP $status, Expected 404)"
    }
} catch {
    Write-Host "[INFO] /swagger check: $_" -ForegroundColor Green
}

# ============================================================
# 3. Core static assets - must be HTTP 200 with correct Content-Type
# ============================================================
Write-Host "`n--- Testing Core Static Assets (HTTP 200 + Content-Type) ---" -ForegroundColor Yellow
$staticAssets = @(
    @{ Path = "/css/site.css";                       ExpectedTypes = @("text/css") },
    @{ Path = "/js/site.js";                        ExpectedTypes = @("text/javascript", "application/javascript") },
    @{ Path = "/lib/bootstrap/css/bootstrap.min.css"; ExpectedTypes = @("text/css") }
)
foreach ($asset in $staticAssets) {
    $fullUrl = "$baseUrl$($asset.Path)"
    try {
        $resp   = $client.GetAsync($fullUrl).GetAwaiter().GetResult()
        $status = [int]$resp.StatusCode
        $ct     = if ($resp.Content.Headers.ContentType) { $resp.Content.Headers.ContentType.MediaType } else { "" }
        $okStatus = ($status -eq 200)
        $okType   = $ct -and ($asset.ExpectedTypes | Where-Object { $ct.ToLower().StartsWith($_.ToLower()) })
        if ($okStatus -and $okType) {
            Write-Host "  [OK] $($asset.Path) => $status, Content-Type: $ct" -ForegroundColor Green
        } elseif (-not $okStatus) {
            Write-Host "  [FAIL] $($asset.Path) => HTTP $status (expected 200)" -ForegroundColor Red
            $failedUrls += "$($asset.Path) (HTTP $status, expected 200)"
        } else {
            Write-Host "  [FAIL] $($asset.Path) => HTTP $status but Content-Type '$ct' != '$($asset.ExpectedType)'" -ForegroundColor Red
            $failedUrls += "$($asset.Path) (wrong Content-Type: $ct)"
        }
    } catch {
        Write-Host "  [ERROR] $($asset.Path) - $_" -ForegroundColor Red
        $failedUrls += "$($asset.Path) ($($_.Exception.Message))"
    }
}

# ============================================================
# 4. Every image URL found in the home page HTML - must return 200
# ============================================================
Write-Host "`n--- Testing Home-Page Image URLs ($($homePageImgSet.Count) unique images in '/') ---" -ForegroundColor Yellow
$homeImgFailed = 0
$homeImgIdx    = 1
foreach ($img in $homePageImgSet) {
    try {
        $resp   = $client.GetAsync($img).GetAwaiter().GetResult()
        $status = [int]$resp.StatusCode
        if ($status -eq 200) {
            Write-Host "  [HOME-IMG $homeImgIdx/$($homePageImgSet.Count)] [200 OK] $img" -ForegroundColor DarkGray
        } else {
            Write-Host "  [HOME-IMG $homeImgIdx/$($homePageImgSet.Count)] [FAIL $status] $img" -ForegroundColor Red
            $homeImgFailed++
            $failedImages += "$img (HTTP $status)"
        }
    } catch {
        Write-Host "  [HOME-IMG $homeImgIdx/$($homePageImgSet.Count)] [ERR] $img - $_" -ForegroundColor Red
        $homeImgFailed++
        $failedImages += "$img ($($_.Exception.Message))"
    }
    $homeImgIdx++
}
$homeColor = if ($homeImgFailed -eq 0) { "Green" } else { "Red" }
Write-Host "  => Home-page images: $($homePageImgSet.Count) checked, $homeImgFailed failed." -ForegroundColor $homeColor

# ============================================================
# 5. All extracted images from all pages
# ============================================================
Write-Host "`n--- Testing All Extracted Images ($($checkedImages.Count) unique assets across all pages) ---" -ForegroundColor Yellow
$imgIdx = 1
foreach ($img in $checkedImages) {
    try {
        $resp   = $client.GetAsync($img).GetAwaiter().GetResult()
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

# ============================================================
# Summary
# ============================================================
Write-Host "`n======================================================================" -ForegroundColor Cyan
Write-Host " SMOKE TEST SUMMARY" -ForegroundColor Cyan
Write-Host "======================================================================" -ForegroundColor Cyan
Write-Host "Endpoints checked: 25 (+swagger gate +3 static asset checks)"
Write-Host "Endpoint failures: $($failedUrls.Count)"
Write-Host "Images checked:    $($checkedImages.Count) total | $($homePageImgSet.Count) from home page"
Write-Host "Image failures:    $($failedImages.Count) total | $homeImgFailed from home page"

$uniqueFailedImages = $failedImages | Sort-Object -Unique

if ($failedUrls.Count -gt 0 -or $uniqueFailedImages.Count -gt 0) {
    Write-Host "`nFAILURES DETECTED:" -ForegroundColor Red
    foreach ($f in $failedUrls)         { Write-Host "  Endpoint: $f" -ForegroundColor Red }
    foreach ($f in $uniqueFailedImages) { Write-Host "  Image:    $f" -ForegroundColor Red }
    exit 1
} else {
    Write-Host "`nALL ENDPOINTS, STATIC ASSET TYPES, AND ALL IMAGES PASSED WITH ZERO ERRORS!" -ForegroundColor Green
    exit 0
}
