# تشغيل كمسؤول مرة واحدة على السيرفر لفتح منفذ القارئة
# Run as Administrator once on the server

$ErrorActionPreference = "Stop"
$port = 5260
$ruleName = "PMS ZKTeco ADMS HTTP $port"

if (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue) {
    Write-Host "Firewall rule already exists: $ruleName"
} else {
    New-NetFirewallRule -DisplayName $ruleName `
        -Direction Inbound `
        -Protocol TCP `
        -LocalPort $port `
        -Action Allow `
        -Profile Any | Out-Null
    Write-Host "Created firewall rule: $ruleName (TCP $port inbound)"
}

Write-Host "Done. Reader ADMS URL example: http://<SERVER-IP>:$port/iclock"
