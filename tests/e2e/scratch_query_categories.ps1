$con = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\mssqllocaldb;Database=AyurvedaDb;Trusted_Connection=True;')
$con.Open()
$cmd = $con.CreateCommand()
$cmd.CommandText = 'SELECT Id, Name FROM Categories'
$r = $cmd.ExecuteReader()
while ($r.Read()) {
    Write-Host "$($r[0]): $($r[1])"
}
$con.Close()
