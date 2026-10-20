@echo off
chcp 65001 >nul
set "DOTNET_ROOT=%LOCALAPPDATA%\\Microsoft\\dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"
dotnet "%~dp0LaserWelderApp.dll"
pause
