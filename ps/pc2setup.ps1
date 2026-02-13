# This script is run on the Radar PC and installs the VC Redist (optional, but helps with some users' runtime errors)

Write-Host "- Checking for admin rights..." -ForegroundColor Yellow
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERROR: This script requires administrative privileges. Please run as administrator." -ForegroundColor Red
    exit 1
}
Write-Host "- Confirmed admin rights" -ForegroundColor Green

Write-Host "- Downloading VC Redist..." -ForegroundColor Yellow
try {
    # Ensure TLS 1.2
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    Invoke-WebRequest -Uri "https://aka.ms/vc14/vc_redist.x64.exe" -OutFile ".\VC_redist.x64.exe" -UseBasicParsing
}
catch {
    Write-Host "ERROR: Failed to download VC redist. $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host "- VC Redist Downloaded!" -ForegroundColor Green
Write-Host "- Installing VC 2015+ Redist..." -ForegroundColor Yellow
$proc = Start-Process -FilePath ".\VC_redist.x64.exe" -ArgumentList "/install", "/quiet", "/norestart" -Wait -PassThru
$ret = $proc.ExitCode

if ($ret -ne 0 -and $ret -ne 1638 -and $ret -ne 3010) {
    Write-Host "- An ERROR has occurred! (Error code: $ret)" -ForegroundColor Red
    exit 1
}

Write-Host "- Setup Completed!" -ForegroundColor Green
exit 0
