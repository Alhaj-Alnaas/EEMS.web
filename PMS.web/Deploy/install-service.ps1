#Requires -RunAsAdministrator
<#
  تثبيت PMS كخدمة Windows تعمل تلقائياً مع إقلاع السيرفر.
  شغّل من مجلد النشر على السيرفر (كمسؤول).
#>
$ErrorActionPreference = "Stop"

$installDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$exePath = Join-Path $installDir "PMS.web.exe"
$serviceName = "PMSWebHost"
$displayName = "PMS Web Host"
$port = 5260

if (-not (Test-Path $exePath)) {
    throw "PMS.web.exe not found in: $installDir"
}

# Firewall
$ruleName = "PMS ZKTeco ADMS HTTP $port"
if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName `
        -Direction Inbound -Protocol TCP -LocalPort $port `
        -Action Allow -Profile Any | Out-Null
    Write-Host "Firewall rule created: TCP $port"
} else {
    Write-Host "Firewall rule already exists: TCP $port"
}

$existing = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Service already exists. Restarting..."
    Restart-Service -Name $serviceName -Force
} else {
    # Environment for the service process
    # sc.exe does not set env vars; we rely on appsettings.Production.json Kestrel binding.
    New-Service `
        -Name $serviceName `
        -BinaryPathName "`"$exePath`"" `
        -DisplayName $displayName `
        -Description "PMS web application + ZKTeco ADMS/Push on port $port" `
        -StartupType Automatic | Out-Null

    # Ensure Production environment for the service
    New-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Services\$serviceName" `
        -Name "Environment" -PropertyType MultiString `
        -Value @(
            "ASPNETCORE_ENVIRONMENT=Production",
            "ASPNETCORE_URLS=http://0.0.0.0:$port"
        ) -Force | Out-Null

    Start-Service -Name $serviceName
}

Start-Sleep -Seconds 2
$svc = Get-Service -Name $serviceName
Write-Host ""
Write-Host "========================================"
Write-Host " Service : $serviceName ($($svc.Status))"
Write-Host " Folder  : $installDir"
Write-Host " Browse  : http://<SERVER-IP>:$port"
Write-Host " Reader  : http://<SERVER-IP>:$port/iclock"
Write-Host "========================================"
Write-Host "Manage: services.msc  |  Stop: Stop-Service $serviceName"
