<# : Moon Sweeper Mahjong installer. This first part is a Windows batch script that runs the rest with PowerShell.
@echo off
setlocal
title Moon Sweeper Mahjong
set "MSM_SELF=%~f0"
set "MSM_HOME=%~dp0"
powershell -NoProfile -ExecutionPolicy Bypass -Command "& ([ScriptBlock]::Create([IO.File]::ReadAllText($env:MSM_SELF))) %*"
set "MSM_CODE=%ERRORLEVEL%"
if not defined MSM_NOPAUSE (
    echo.
    pause
)
exit /b %MSM_CODE%
#>
