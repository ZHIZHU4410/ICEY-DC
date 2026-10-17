@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Run-Playtest.ps1"
if errorlevel 1 pause
