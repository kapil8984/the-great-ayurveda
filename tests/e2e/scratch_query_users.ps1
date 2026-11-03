Add-Type -AssemblyName 'System.Data'
$conn = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\mssqllocaldb;Database=AyurvedaDb;Trusted_Connection=True;')
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "UPDATE AspNetUsers SET IsActive = 1, EmailConfirmed = 1 WHERE Email = 'demo.doctor@thegreatayurveda.com'"
$rows = $cmd.ExecuteNonQuery()
Write-Host "demo.doctor reactivated (rows: $rows)"
$conn.Close()
