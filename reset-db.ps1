<#
.SYNOPSIS
    Resets the AyurvedaDb SQL Server database for fresh development or test runs.
.DESCRIPTION
    Safely terminates connections and drops the AyurvedaDb database so that
    Ayurveda.Web can perform fresh migrations and seeding on its next startup.
#>
param(
    [string]$Server = "(localdb)\mssqllocaldb",
    [string]$Database = "AyurvedaDb"
)

$ErrorActionPreference = "Stop"

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Resetting Database: $Database on $Server" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. Start LocalDB if applicable
if ($Server -like "*(localdb)*") {
    $sqllocaldb = Get-Command sqllocaldb -ErrorAction SilentlyContinue
    if ($sqllocaldb) {
        Write-Host "Ensuring LocalDB instance 'mssqllocaldb' is started..." -ForegroundColor Yellow
        & sqllocaldb start mssqllocaldb | Out-Null
    }
}

# 2. Drop the database
$dropSql = @"
IF DB_ID('$Database') IS NOT NULL
BEGIN
    ALTER DATABASE [$Database] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE [$Database];
    PRINT 'Database [$Database] dropped successfully.';
END
ELSE
BEGIN
    PRINT 'Database [$Database] does not exist. Nothing to drop.';
END
"@

$sqlcmd = Get-Command sqlcmd -ErrorAction SilentlyContinue
if ($sqlcmd) {
    Write-Host "Executing database drop using sqlcmd..." -ForegroundColor Cyan
    & sqlcmd -S "$Server" -d "master" -Q "$dropSql"
} else {
    Write-Host "Executing database drop using ADO.NET..." -ForegroundColor Cyan
    $connStr = "Server=$Server;Database=master;Trusted_Connection=True;TrustServerCertificate=True;"
    $conn = New-Object Microsoft.Data.SqlClient.SqlConnection($connStr)
    $conn.Open()
    $cmd = $conn.CreateCommand()
    $cmd.CommandText = $dropSql
    $cmd.ExecuteNonQuery() | Out-Null
    $conn.Close()
}

Write-Host "Reset complete. Next run of Ayurveda.Web will create, migrate, and seed the database." -ForegroundColor Green
