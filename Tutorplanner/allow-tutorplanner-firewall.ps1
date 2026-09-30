$ErrorActionPreference = 'Stop'
New-NetFirewallRule -DisplayName 'Tutorplanner Local Network 5167' -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5167 -Profile Private -Description 'Allow Tutorplanner from devices on the private local network.'
Write-Host 'Tutorplanner firewall access enabled for Private networks on TCP port 5167.'
Read-Host 'Press Enter to close'
