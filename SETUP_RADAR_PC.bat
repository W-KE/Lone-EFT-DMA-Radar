@ECHO OFF
CD /D "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\ps\pc2setup.ps1"
PAUSE
EXIT 0