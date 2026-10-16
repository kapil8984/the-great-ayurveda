# ==============================================================================
# publish-live.ps1 - Production Publish & Asset Verification Script
# ==============================================================================
$ErrorActionPreference = "Stop"

Write-Host "==> [1/6] Cleaning src/Ayurveda.Web/wwwroot/uploads of any test files..." -ForegroundColor Cyan
$uploadsDir = Join-Path $PSScriptRoot "src/Ayurveda.Web/wwwroot/uploads"
if (Test-Path $uploadsDir) {
    Get-ChildItem -Path $uploadsDir -File | Remove-Item -Force
} else {
    New-Item -ItemType Directory -Path $uploadsDir -Force | Out-Null
}

$publishTarget = Join-Path $PSScriptRoot "publish/Web"
if (Test-Path $publishTarget) {
    Write-Host "==> Cleaning prior publish folder..." -ForegroundColor Cyan
    Remove-Item -Recurse -Force $publishTarget
}

Write-Host "==> [2/6] Running dotnet publish (Release, win-x64, no-self-contained)..." -ForegroundColor Cyan
$csproj = Join-Path $PSScriptRoot "src/Ayurveda.Web/Ayurveda.Web.csproj"
& dotnet publish $csproj -c Release -r win-x64 --no-self-contained -o $publishTarget
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Host "==> [3/6] Ensuring catalog_300plus.json is present in publish directory..." -ForegroundColor Cyan
$publishCatalog = Join-Path $publishTarget "catalog_300plus.json"
if (-not (Test-Path $publishCatalog)) {
    $sourceCatalog = Join-Path $PSScriptRoot "src/Ayurveda.Infrastructure/Data/catalog_300plus.json"
    if (Test-Path $sourceCatalog) {
        Copy-Item -Path $sourceCatalog -Destination $publishCatalog -Force
    }
}

Write-Host "==> [4/6] Removing Development appsettings and cleaning uploads in publish output..." -ForegroundColor Cyan
$devSettings = Join-Path $publishTarget "appsettings.Development.json"
if (Test-Path $devSettings) {
    Remove-Item -Force $devSettings
    Write-Host "Removed appsettings.Development.json from publish directory" -ForegroundColor Yellow
}

$publishUploads = Join-Path $publishTarget "wwwroot/uploads"
if (Test-Path $publishUploads) {
    Get-ChildItem -Path $publishUploads -File | Remove-Item -Force
}

Write-Host "==> [5/6] Verifying Production publish requirements..." -ForegroundColor Cyan
$webConfig = Join-Path $publishTarget "web.config"
$imagesDir = Join-Path $publishTarget "wwwroot/images"

if (-not (Test-Path $webConfig)) { throw "ASSERTION FAILED: web.config is missing in $publishTarget" }
if (-not (Test-Path $publishCatalog)) { throw "ASSERTION FAILED: catalog_300plus.json is missing in $publishTarget" }
if (-not (Test-Path $imagesDir)) { throw "ASSERTION FAILED: wwwroot/images is missing in $publishTarget" }
if (Test-Path $devSettings) { throw "ASSERTION FAILED: appsettings.Development.json must NOT exist in production publish!" }

$remainingUploads = if (Test-Path $publishUploads) { (Get-ChildItem -Path $publishUploads -File).Count } else { 0 }
if ($remainingUploads -gt 0) { throw "ASSERTION FAILED: publish wwwroot/uploads must be empty, found $remainingUploads files!" }

$efDesignDll = Join-Path $publishTarget "Microsoft.EntityFrameworkCore.Design.dll"
$swashbuckleDll = Join-Path $publishTarget "Swashbuckle.AspNetCore.Swagger.dll"
if (Test-Path $efDesignDll) { throw "ASSERTION FAILED: Microsoft.EntityFrameworkCore.Design.dll must be excluded from publish!" }
if (Test-Path $swashbuckleDll) { throw "ASSERTION FAILED: Swashbuckle must NOT be published in Release configuration!" }

Write-Host "    [OK] web.config verified" -ForegroundColor Green
Write-Host "    [OK] catalog_300plus.json verified" -ForegroundColor Green
Write-Host "    [OK] wwwroot/images verified" -ForegroundColor Green
Write-Host "    [OK] No appsettings.Development.json" -ForegroundColor Green
Write-Host "    [OK] No upload test files" -ForegroundColor Green
Write-Host "    [OK] No EF Design/Tools or Swashbuckle assemblies" -ForegroundColor Green

Write-Host "==> [6/6] Calculating publish folder size..." -ForegroundColor Cyan
$totalBytes = (Get-ChildItem -Path $publishTarget -Recurse -File | Measure-Object -Property Length -Sum).Sum
$totalMB = [math]::Round($totalBytes / 1MB, 2)
Write-Host "==> Production Publish Succeeded! Total folder size: $totalMB MB ($totalBytes bytes) at: $publishTarget" -ForegroundColor Green
