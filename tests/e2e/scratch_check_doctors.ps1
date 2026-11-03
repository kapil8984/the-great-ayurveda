Add-Type -AssemblyName 'System.Data'
$conn = New-Object System.Data.SqlClient.SqlConnection('Server=(localdb)\mssqllocaldb;Database=AyurvedaDb;Trusted_Connection=True;')
$conn.Open()
$cmd = $conn.CreateCommand()
$cmd.CommandText = "SELECT COUNT(*) as Total FROM Doctors WHERE IsDeleted = 0"
$total = $cmd.ExecuteScalar()
Write-Host "Total Active Doctors in DB: $total"

$cmd.CommandText = "SELECT TOP 5 d.Id, d.DoctorName, d.Email, d.ApplicationUserId, dd.Status FROM Doctors d LEFT JOIN DoctorDetails dd ON d.Id = dd.DoctorId ORDER BY d.Id DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Doc: ID=$($reader['Id']) Name=$($reader['DoctorName']) Email=$($reader['Email']) AppUserId=$($reader['ApplicationUserId']) Status=$($reader['Status'])"
}
$reader.Close()

$cmd.CommandText = "SELECT TOP 5 a.Id, a.DoctorId, a.UserId, a.PatientName, a.AppointmentDate, a.StartUtc, a.Status, a.ConsultationType, a.MeetingRoomId FROM Appointments a ORDER BY a.Id DESC"
$reader = $cmd.ExecuteReader()
while ($reader.Read()) {
    Write-Host "Appt: ID=$($reader['Id']) DocId=$($reader['DoctorId']) UserId=$($reader['UserId']) Patient=$($reader['PatientName']) Date=$($reader['AppointmentDate']) StartUtc=$($reader['StartUtc']) Status=$($reader['Status']) Type=$($reader['ConsultationType']) Room=$($reader['MeetingRoomId'])"
}
$reader.Close()
$conn.Close()
