$conn = Get-NetTCPConnection -LocalPort 5099 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -First 1
if ($conn) {
    $proc = Get-Process -Id $conn -ErrorAction SilentlyContinue
    if ($proc) {
        $ws = [math]::Round($proc.WorkingSet64 / 1MB, 2)
        Write-Output $ws
    }
}
