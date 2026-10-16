$conn = Get-NetTCPConnection -LocalPort 5099 -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -First 1
if ($conn) {
    $proc = Get-Process -Id $conn -ErrorAction SilentlyContinue
    if ($proc) {
        $before = [math]::Round($proc.WorkingSet64 / 1MB, 2)
        $code = '[DllImport("psapi.dll")] public static extern bool EmptyWorkingSet(IntPtr hProcess);'
        Add-Type -MemberDefinition $code -Name PsApi -Namespace Win32 -ErrorAction SilentlyContinue
        [Win32.PsApi]::EmptyWorkingSet($proc.Handle) | Out-Null
        $proc.Refresh()
        $after = [math]::Round($proc.WorkingSet64 / 1MB, 2)
        Write-Output "Before: $before MB | After: $after MB"
    }
}
