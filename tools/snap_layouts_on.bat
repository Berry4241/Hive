@echo off
rem Turns Windows' Snap layouts on maximize-button hover back ON (the Windows default). Undo of snap_layouts_off.bat
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0snap_layouts.ps1" -State on
pause
