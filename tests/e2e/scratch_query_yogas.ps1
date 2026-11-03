$con = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\mssqllocaldb;Database=AyurvedaDb;Trusted_Connection=True;')
$con.Open()
$cmd = $con.CreateCommand()
$cmd.CommandText = 'SELECT Id, YogaName, ImageUrl FROM Yogas'
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Host "$($r[0]): $($r[1]) -> $($r[2])"
}
$con.Close()
