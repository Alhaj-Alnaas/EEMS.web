#Requires -RunAsAdministrator
<#
  إيقاف وإزالة خدمة PMS من السيرفر.
#>
$ErrorActionPreference = "Stop"
$serviceName = "PMSWebHost"

$svc = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if (-not $svc) {
    Write-Host "Service not found: $serviceName"
    exit 0
}

if ($svc.Status -ne "Stopped") {
    Stop-Service -Name $serviceName -Force
    Start-Sleep -Seconds 2
}

sc.exe delete $serviceName | Out-Null
Write-Host "Service removed: $serviceName"
