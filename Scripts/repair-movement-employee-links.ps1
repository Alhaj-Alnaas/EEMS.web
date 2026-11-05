$cs = "Server=10.10.102.4;Database=PMSDB;User Id=sa;Password=Mr5@24Li5sc0;TrustServerCertificate=True"
$cn = New-Object System.Data.SqlClient.SqlConnection($cs)
$cn.Open()
$cmd = $cn.CreateCommand()
$cmd.CommandText = "SELECT Id, DevicePin, remarks FROM MovementLogs WHERE isDeleted=0 AND (EmployeeId IS NULL OR DevicePin LIKE 'time=%')"
$r = $cmd.ExecuteReader()
$rows = New-Object System.Collections.Generic.List[object]
while ($r.Read()) {
  $rem = if ($r.IsDBNull(2)) { "" } else { $r.GetString(2) }
  $rows.Add([pscustomobject]@{ Id = $r.GetGuid(0); Pin = $r.GetString(1); Rem = $rem })
}
$r.Close()
$fixed = 0
foreach ($row in $rows) {
  $m = [regex]::Match($row.Rem, '(?i)(?:^|[\t\s])pin=([^\t\s]+)')
  if (-not $m.Success) { continue }
  $pin = $m.Groups[1].Value.Trim()
  if ($pin -eq "" -or $pin -eq "0") { continue }
  $upd = $cn.CreateCommand()
  $upd.CommandText = "UPDATE ml SET ml.DevicePin=@pin, ml.EmployeeId=e.Id, ml.updatedOn=GETDATE() FROM MovementLogs ml INNER JOIN Employees e ON e.isDeleted=0 AND LTRIM(RTRIM(e.FileNumber))=@pin WHERE ml.Id=@id"
  [void]$upd.Parameters.AddWithValue("@pin", $pin)
  [void]$upd.Parameters.AddWithValue("@id", $row.Id)
  $n = $upd.ExecuteNonQuery()
  if ($n -gt 0) { $fixed++ }
}
Write-Output "Candidates=$($rows.Count) Fixed=$fixed"
$chk = $cn.CreateCommand()
$chk.CommandText = "SELECT TOP 5 DevicePin, CAST(EmployeeId AS varchar(36)) AS EmployeeId FROM MovementLogs WHERE DevicePin='1112' ORDER BY EventTime DESC"
$rr = $chk.ExecuteReader()
while ($rr.Read()) {
  Write-Output ("Pin=" + $rr.GetString(0) + " Emp=" + $(if ($rr.IsDBNull(1)) { "NULL" } else { $rr.GetString(1) }))
}
$rr.Close()
$cn.Close()
