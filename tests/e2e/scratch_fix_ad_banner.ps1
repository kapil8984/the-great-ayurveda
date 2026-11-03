$con = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\mssqllocaldb;Database=AyurvedaDb;Trusted_Connection=True;')
$con.Open()
$cmd = $con.CreateCommand()
$cmd.CommandText = "UPDATE Banners SET ImageUrl = '/images/banners/ad-ghee-inline.webp' WHERE ImageUrl LIKE '%vaidya-shastra-classical-guduchi-a2-gir-cow-bilona-ghee-formula-3%'"
$rows = $cmd.ExecuteNonQuery()
Write-Host "Updated rows: $rows"
$con.Close()
