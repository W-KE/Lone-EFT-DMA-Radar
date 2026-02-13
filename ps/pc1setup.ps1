# This script is on the Game PC and Disables Fast Boot and Memory Compression. Reduces paging out and VMM Init Error.

Write-Host "- Checking for admin rights..." -ForegroundColor Yellow
$principal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "ERROR: This script requires administrative privileges. Please run as administrator." -ForegroundColor Red
    exit 1
}
Write-Host "- Confirmed admin rights" -ForegroundColor Green

Write-Host "- Disabling Fast Boot" -ForegroundColor Yellow
try {
    Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Power" -Name "HiberbootEnabled" -Type DWord -Value 0 -Force
}
catch {
    Write-Host "- Failed to disable Fast Boot! $($_.Exception.Message)" -ForegroundColor Red
    exit 1
}
Write-Host "- Fast Boot Disabled!" -ForegroundColor Green

Write-Host "- Disabling Memory Compression..." -ForegroundColor Yellow
try {
    Disable-MMAgent -MemoryCompression
}
catch {
    Write-Host "WARNING: Memory Compression may be already disabled. Please verify it is set to False below:" -ForegroundColor DarkYellow
    Get-MMAgent
    PAUSE
}
Write-Host "- Memory Compression Disabled!" -ForegroundColor Green

SHUTDOWN /r /t 10 /c "Success! Your computer will now reboot..."
exit 0
