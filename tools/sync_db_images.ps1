# tools/sync_db_images.ps1
# Synchronize Genuine Product Photographs & Angles into MSSQLLocalDB AyurvedaDb

$ErrorActionPreference = "Stop"

$catalogFile = Join-Path $PSScriptRoot "..\src\Ayurveda.Infrastructure\Data\catalog_300plus.json"
if (-not (Test-Path $catalogFile)) {
    Write-Error "Catalog file not found at: $catalogFile"
}

$catalog = Get-Content -Path $catalogFile -Raw | ConvertFrom-Json
Write-Host "Loaded $($catalog.Count) products from catalog_300plus.json" -ForegroundColor Cyan

$connStr = "Server=(localdb)\mssqllocaldb;Database=AyurvedaDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($connStr)
$conn.Open()

Write-Host "Connected to AyurvedaDb on (localdb)\mssqllocaldb" -ForegroundColor Green

$cmdUpdateProd = $conn.CreateCommand()
$cmdUpdateProd.CommandText = @"
UPDATE Products 
SET ImageUrl = @ImageUrl
WHERE Id = @Id;
"@
$pImg = $cmdUpdateProd.Parameters.Add("@ImageUrl", [System.Data.SqlDbType]::NVarChar, 1000)
$paramProdId = $cmdUpdateProd.Parameters.Add("@Id", [System.Data.SqlDbType]::Int)

$cmdDeleteImg = $conn.CreateCommand()
$cmdDeleteImg.CommandText = "DELETE FROM ProductImages WHERE ProductId = @ProductId;"
$pDelProdId = $cmdDeleteImg.Parameters.Add("@ProductId", [System.Data.SqlDbType]::Int)

$cmdInsertImg = $conn.CreateCommand()
$cmdInsertImg.CommandText = @"
INSERT INTO ProductImages (ProductId, ImageUrl, IsPrimary, DisplayOrder)
VALUES (@ProductId, @ImageUrl, @IsPrimary, @DisplayOrder);
"@
$pInsProdId = $cmdInsertImg.Parameters.Add("@ProductId", [System.Data.SqlDbType]::Int)
$pInsUrl = $cmdInsertImg.Parameters.Add("@ImageUrl", [System.Data.SqlDbType]::NVarChar, 1000)
$pInsPrim = $cmdInsertImg.Parameters.Add("@IsPrimary", [System.Data.SqlDbType]::Bit)
$pInsOrder = $cmdInsertImg.Parameters.Add("@DisplayOrder", [System.Data.SqlDbType]::Int)

$totalUpdated = 0
$totalImagesInserted = 0
$transaction = $conn.BeginTransaction()
$cmdUpdateProd.Transaction = $transaction
$cmdDeleteImg.Transaction = $transaction
$cmdInsertImg.Transaction = $transaction

try {
    for ($i = 0; $i -lt $catalog.Count; $i++) {
        $p = $catalog[$i]
        $prodId = $i + 1
        $imgUrl = if ($p.imageUrl) { $p.imageUrl } else { "/images/placeholder.svg" }

        # Update product
        $pImg.Value = $imgUrl
        $paramProdId.Value = $prodId
        $cmdUpdateProd.ExecuteNonQuery() | Out-Null
        $totalUpdated++

        # Delete old images for this product
        $pDelProdId.Value = $prodId
        $cmdDeleteImg.ExecuteNonQuery() | Out-Null

        # Insert primary image
        $pInsProdId.Value = $prodId
        $pInsUrl.Value = $imgUrl
        $pInsPrim.Value = $true
        $pInsOrder.Value = 1
        $cmdInsertImg.ExecuteNonQuery() | Out-Null
        $totalImagesInserted++

        # Insert additional angle images
        if ($p.additionalImages) {
            $order = 2
            foreach ($addImg in $p.additionalImages) {
                if ($addImg -and $addImg -ne $imgUrl) {
                    $pInsProdId.Value = $prodId
                    $pInsUrl.Value = $addImg
                    $pInsPrim.Value = $false
                    $pInsOrder.Value = $order
                    $cmdInsertImg.ExecuteNonQuery() | Out-Null
                    $totalImagesInserted++
                    $order++
                }
            }
        }
    }

    $transaction.Commit()
    Write-Host "Transaction committed successfully!" -ForegroundColor Green
    Write-Host "Total Products Updated: $totalUpdated" -ForegroundColor Green
    Write-Host "Total Product Image Angles Inserted into ProductImages: $totalImagesInserted" -ForegroundColor Green
}
catch {
    $transaction.Rollback()
    Write-Error "Failed to synchronize product images: $_"
}
finally {
    $conn.Close()
}

