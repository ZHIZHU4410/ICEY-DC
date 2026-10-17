@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Stop-Playtest.ps1"
if errorlevel 1 pause
